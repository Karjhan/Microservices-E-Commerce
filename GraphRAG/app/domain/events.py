from dataclasses import dataclass, field
from datetime import datetime


@dataclass
class ProductEvent:
    """Base representation of an incoming catalog event."""

    product_id: str
    event_type: str
    timestamp: datetime = field(default_factory=datetime.utcnow)
    payload: dict = field(default_factory=dict)
