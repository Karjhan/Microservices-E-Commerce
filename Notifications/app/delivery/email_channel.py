from email.message import EmailMessage

import aiosmtplib

from app.core.settings import get_settings
from app.delivery.base import PermanentDeliveryError, TransientDeliveryError


class EmailChannel:
    async def send(
        self,
        recipient: str,
        subject: str,
        body_html: str,
        body_text: str,
    ) -> None:
        settings = get_settings()
        message = EmailMessage()
        message["From"] = settings.smtp_from
        message["To"] = recipient
        message["Subject"] = subject
        message.set_content(body_text or body_html)
        if body_html:
            message.add_alternative(body_html, subtype="html")

        try:
            await aiosmtplib.send(
                message,
                hostname=settings.smtp_host,
                port=settings.smtp_port,
                use_tls=settings.smtp_use_tls,
                start_tls=not settings.smtp_use_tls and settings.smtp_port == 587,
                username=settings.smtp_username,
                password=settings.smtp_password,
                timeout=30,
            )
        except aiosmtplib.SMTPRecipientsRefused as exc:
            raise PermanentDeliveryError(str(exc)) from exc
        except aiosmtplib.SMTPAuthenticationError as exc:
            raise PermanentDeliveryError(str(exc)) from exc
        except aiosmtplib.SMTPException as exc:
            raise TransientDeliveryError(str(exc)) from exc
        except OSError as exc:
            raise TransientDeliveryError(str(exc)) from exc
