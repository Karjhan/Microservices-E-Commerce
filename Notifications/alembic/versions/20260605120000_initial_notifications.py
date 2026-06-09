"""Initial notifications schema

Revision ID: 20260605120000
Revises:
Create Date: 2026-06-05 12:00:00
"""

from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision: str = "20260605120000"
down_revision: Union[str, None] = None
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "notifications",
        sa.Column("id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("idempotency_key", sa.String(length=256), nullable=False),
        sa.Column("event_type", sa.String(length=64), nullable=False),
        sa.Column("notification_type", sa.String(length=64), nullable=False),
        sa.Column("channel", sa.String(length=16), nullable=False),
        sa.Column("recipient", sa.String(length=320), nullable=False),
        sa.Column("subject", sa.String(length=500), nullable=False),
        sa.Column("body_preview", sa.String(length=500), nullable=True),
        sa.Column("status", sa.String(length=32), nullable=False),
        sa.Column("attempt_count", sa.Integer(), nullable=False),
        sa.Column("correlation_id", sa.String(length=64), nullable=True),
        sa.Column("error_details", sa.Text(), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.text("now()"), nullable=False),
        sa.Column("sent_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("updated_at", sa.DateTime(timezone=True), server_default=sa.text("now()"), nullable=False),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("idempotency_key"),
    )
    op.create_index("ix_notifications_event_type_created_at", "notifications", ["event_type", "created_at"], unique=False)
    op.create_index("ix_notifications_recipient_created_at", "notifications", ["recipient", "created_at"], unique=False)
    op.create_index("ix_notifications_status", "notifications", ["status"], unique=False)

    op.create_table(
        "processed_event_logs",
        sa.Column("id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("idempotency_key", sa.String(length=256), nullable=False),
        sa.Column("event_type", sa.String(length=64), nullable=False),
        sa.Column("processed_at", sa.DateTime(timezone=True), server_default=sa.text("now()"), nullable=False),
        sa.Column("outcome", sa.String(length=32), nullable=False),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("idempotency_key"),
    )
    op.create_index("ix_processed_event_logs_idempotency_key", "processed_event_logs", ["idempotency_key"], unique=True)


def downgrade() -> None:
    op.drop_index("ix_processed_event_logs_idempotency_key", table_name="processed_event_logs")
    op.drop_table("processed_event_logs")
    op.drop_index("ix_notifications_status", table_name="notifications")
    op.drop_index("ix_notifications_recipient_created_at", table_name="notifications")
    op.drop_index("ix_notifications_event_type_created_at", table_name="notifications")
    op.drop_table("notifications")
