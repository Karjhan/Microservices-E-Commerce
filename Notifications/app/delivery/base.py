from dataclasses import dataclass
from typing import Protocol


@dataclass
class RenderedTemplate:
    subject: str
    body_html: str
    body_text: str


class DeliveryChannel(Protocol):
    async def send(
        self,
        recipient: str,
        subject: str,
        body_html: str,
        body_text: str,
    ) -> None:
        """Send a notification. Raises TransientDeliveryError or PermanentDeliveryError."""


class TransientDeliveryError(Exception):
    """Retryable delivery failure (network, temporary SMTP rejection)."""


class PermanentDeliveryError(Exception):
    """Non-retryable delivery failure (invalid recipient, auth failure)."""
