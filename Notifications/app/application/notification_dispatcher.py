import json
import uuid

from aio_pika import Message
from sqlalchemy.ext.asyncio import AsyncSession
from tenacity import retry, retry_if_exception_type, stop_after_attempt, wait_exponential_jitter

from app.application.idempotency_service import IdempotencyService
from app.application.template_renderer import TemplateRenderer
from app.core.logging import get_logger
from app.core.settings import get_settings
from app.delivery.base import (
    DeliveryChannel,
    PermanentDeliveryError,
    TransientDeliveryError,
)
from app.delivery.email_channel import EmailChannel
from app.domain.enums import ChannelType, DeliveryStatus, NotificationType, ProcessedOutcome
from app.persistence.models import Notification
from app.persistence.repositories import NotificationRepository, utc_now

logger = get_logger(__name__)


class NotificationDispatcher:
    def __init__(
        self,
        *,
        channel: DeliveryChannel | None = None,
        renderer: TemplateRenderer | None = None,
        idempotency: IdempotencyService | None = None,
        notification_repo: NotificationRepository | None = None,
    ) -> None:
        self._channel = channel or EmailChannel()
        self._renderer = renderer or TemplateRenderer()
        self._idempotency = idempotency or IdempotencyService()
        self._notifications = notification_repo or NotificationRepository()
        self._settings = get_settings()
        self._dlq_channel = None
        self._dlx_exchange = None

    def set_dlq_channel(self, channel, dlx_exchange) -> None:
        self._dlq_channel = channel
        self._dlx_exchange = dlx_exchange

    async def dispatch(
        self,
        session: AsyncSession,
        *,
        routing_key: str,
        entity_id: str,
        recipient: str,
        notification_type: NotificationType,
        template_slug: str,
        template_context: dict,
        correlation_id: str | None = None,
    ) -> None:
        idempotency_key = self._idempotency.build_key(routing_key, entity_id)

        if await self._idempotency.is_processed(session, idempotency_key):
            logger.info("Duplicate event skipped", idempotency_key=idempotency_key)
            return

        rendered = self._renderer.render(template_slug, template_context)
        body_preview = (rendered.body_text or rendered.body_html)[:500]

        notification = Notification(
            id=uuid.uuid4(),
            idempotency_key=idempotency_key,
            event_type=routing_key,
            notification_type=notification_type.value,
            channel=ChannelType.EMAIL.value,
            recipient=recipient,
            subject=rendered.subject,
            body_preview=body_preview,
            status=DeliveryStatus.PENDING.value,
            attempt_count=0,
            correlation_id=correlation_id,
        )
        await self._notifications.create(session, notification)

        error: str | None = None
        try:
            await self._send_with_retry(recipient, rendered.subject, rendered.body_html, rendered.body_text)
            notification.status = DeliveryStatus.SENT.value
            notification.sent_at = utc_now()
            notification.attempt_count = max(notification.attempt_count, 1)
        except PermanentDeliveryError as exc:
            error = str(exc)
            notification.status = DeliveryStatus.FAILED.value
            notification.error_details = error
            notification.attempt_count = self._settings.max_delivery_attempts
            await self._idempotency.mark_processed(
                session,
                idempotency_key=idempotency_key,
                event_type=routing_key,
                outcome=ProcessedOutcome.FAILED_PERMANENTLY,
            )
            await session.commit()
            return
        except TransientDeliveryError as exc:
            error = str(exc)
            notification.status = DeliveryStatus.FAILED.value
            notification.error_details = error
            notification.attempt_count = self._settings.max_delivery_attempts
            await self._publish_to_dlq(
                routing_key=routing_key,
                entity_id=entity_id,
                payload=template_context,
                error=error,
                correlation_id=correlation_id,
            )
            notification.status = DeliveryStatus.DEAD_LETTERED.value
            await self._idempotency.mark_processed(
                session,
                idempotency_key=idempotency_key,
                event_type=routing_key,
                outcome=ProcessedOutcome.FAILED_PERMANENTLY,
            )
            await session.commit()
            return

        await self._idempotency.mark_processed(
            session,
            idempotency_key=idempotency_key,
            event_type=routing_key,
            outcome=ProcessedOutcome.SENT,
        )
        await session.commit()
        logger.info(
            "Notification sent",
            idempotency_key=idempotency_key,
            recipient=recipient,
            notification_type=notification_type.value,
        )

    async def dispatch_to_recipients(
        self,
        session: AsyncSession,
        *,
        routing_key: str,
        entity_id: str,
        recipients: list[str],
        notification_type: NotificationType,
        template_slug: str,
        template_context: dict,
        correlation_id: str | None = None,
    ) -> None:
        idempotency_key = self._idempotency.build_key(routing_key, entity_id)

        if await self._idempotency.is_processed(session, idempotency_key):
            logger.info("Duplicate event skipped", idempotency_key=idempotency_key)
            return

        rendered = self._renderer.render(template_slug, template_context)
        body_preview = (rendered.body_text or rendered.body_html)[:500]
        recipient_field = ", ".join(recipients)

        notification = Notification(
            id=uuid.uuid4(),
            idempotency_key=idempotency_key,
            event_type=routing_key,
            notification_type=notification_type.value,
            channel=ChannelType.EMAIL.value,
            recipient=recipient_field,
            subject=rendered.subject,
            body_preview=body_preview,
            status=DeliveryStatus.PENDING.value,
            attempt_count=0,
            correlation_id=correlation_id,
        )
        await self._notifications.create(session, notification)

        try:
            for recipient in recipients:
                await self._send_with_retry(
                    recipient, rendered.subject, rendered.body_html, rendered.body_text
                )
            notification.status = DeliveryStatus.SENT.value
            notification.sent_at = utc_now()
            notification.attempt_count = 1
        except PermanentDeliveryError as exc:
            notification.status = DeliveryStatus.FAILED.value
            notification.error_details = str(exc)
            notification.attempt_count = self._settings.max_delivery_attempts
            await self._idempotency.mark_processed(
                session,
                idempotency_key=idempotency_key,
                event_type=routing_key,
                outcome=ProcessedOutcome.FAILED_PERMANENTLY,
            )
            await session.commit()
            return
        except TransientDeliveryError as exc:
            notification.status = DeliveryStatus.DEAD_LETTERED.value
            notification.error_details = str(exc)
            notification.attempt_count = self._settings.max_delivery_attempts
            await self._publish_to_dlq(
                routing_key=routing_key,
                entity_id=entity_id,
                payload=template_context,
                error=str(exc),
                correlation_id=correlation_id,
            )
            await self._idempotency.mark_processed(
                session,
                idempotency_key=idempotency_key,
                event_type=routing_key,
                outcome=ProcessedOutcome.FAILED_PERMANENTLY,
            )
            await session.commit()
            return

        await self._idempotency.mark_processed(
            session,
            idempotency_key=idempotency_key,
            event_type=routing_key,
            outcome=ProcessedOutcome.SENT,
        )
        await session.commit()

    async def skip_invalid_payload(
        self,
        session: AsyncSession,
        *,
        routing_key: str,
        entity_id: str,
        recipient: str | None,
        notification_type: NotificationType,
        reason: str,
        correlation_id: str | None = None,
    ) -> None:
        idempotency_key = self._idempotency.build_key(routing_key, entity_id)

        if await self._idempotency.is_processed(session, idempotency_key):
            return

        notification = Notification(
            id=uuid.uuid4(),
            idempotency_key=idempotency_key,
            event_type=routing_key,
            notification_type=notification_type.value,
            channel=ChannelType.EMAIL.value,
            recipient=recipient or "",
            subject="(skipped)",
            status=DeliveryStatus.FAILED.value,
            attempt_count=1,
            correlation_id=correlation_id,
            error_details=reason,
        )
        await self._notifications.create(session, notification)
        await self._idempotency.mark_processed(
            session,
            idempotency_key=idempotency_key,
            event_type=routing_key,
            outcome=ProcessedOutcome.SKIPPED_INVALID_PAYLOAD,
        )
        await session.commit()
        logger.warning(
            "Invalid payload skipped",
            idempotency_key=idempotency_key,
            reason=reason,
        )

    async def _send_with_retry(
        self, recipient: str, subject: str, body_html: str, body_text: str
    ) -> None:
        max_attempts = self._settings.max_delivery_attempts

        @retry(
            retry=retry_if_exception_type(TransientDeliveryError),
            stop=stop_after_attempt(max_attempts),
            wait=wait_exponential_jitter(initial=1, max=10),
            reraise=True,
        )
        async def _attempt() -> None:
            await self._channel.send(recipient, subject, body_html, body_text)

        await _attempt()

    async def _publish_to_dlq(
        self,
        *,
        routing_key: str,
        entity_id: str,
        payload: dict,
        error: str,
        correlation_id: str | None,
    ) -> None:
        if self._dlx_exchange is None:
            logger.error("DLQ exchange not configured", routing_key=routing_key)
            return

        body = json.dumps(
            {
                "routingKey": routing_key,
                "entityId": entity_id,
                "payload": payload,
                "error": error,
            }
        ).encode("utf-8")

        await self._dlx_exchange.publish(
            Message(
                body,
                content_type="application/json",
                headers={"X-Correlation-Id": correlation_id} if correlation_id else None,
            ),
            routing_key=self._settings.rabbitmq_dlq,
        )
        logger.warning("Message routed to DLQ", routing_key=routing_key, entity_id=entity_id)
