"""
OpenTelemetry setup for GraphRAG service.

Mirrors the pattern used in the .NET services:
- OTLP exporter → Jaeger
- Auto-instruments FastAPI routes (same as ASP.NET Core instrumentation)
- Auto-instruments httpx (same as HttpClient instrumentation for Ollama calls)
- Auto-instruments aio-pika (RabbitMQ consumer spans)
- Bridges structlog so trace_id/span_id appear in every log line

Usage:
    from app.core.telemetry import configure_telemetry
    configure_telemetry(app)  # called once during FastAPI lifespan startup
"""

from opentelemetry import trace
from opentelemetry.sdk.resources import Resource, SERVICE_NAME
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor
from opentelemetry.instrumentation.aio_pika import AioPikaInstrumentor
from opentelemetry.instrumentation.logging import LoggingInstrumentor
import structlog

from app.core.settings import get_settings

_configured = False


def _otel_trace_context_processor(logger, method_name, event_dict: dict) -> dict:
    """
    structlog processor: injects active OTel trace_id and span_id into every log record.
    This correlates logs with traces in Jaeger the same way the .NET OTel logging bridge does.
    """
    span = trace.get_current_span()
    if span and span.is_recording():
        ctx = span.get_span_context()
        event_dict["trace_id"] = format(ctx.trace_id, "032x")
        event_dict["span_id"] = format(ctx.span_id, "016x")
    return event_dict


def configure_telemetry(fastapi_app) -> None:
    """
    Set up OpenTelemetry tracing with OTLP export to Jaeger.
    Safe to call multiple times — only configures once.
    """
    global _configured
    if _configured:
        return

    settings = get_settings()
    if not settings.otel_enabled:
        return

    # Build the resource (equivalent to ResourceBuilder.CreateDefault().AddService() in .NET)
    resource = Resource.create({SERVICE_NAME: settings.service_name})

    # OTLP exporter → Jaeger (gRPC, same protocol as the .NET services use)
    exporter = OTLPSpanExporter(
        endpoint=settings.otel_endpoint,
        # grpc doesn't need /v1/traces path — Jaeger accepts it directly on 4317
        insecure=True,
    )

    provider = TracerProvider(resource=resource)
    provider.add_span_processor(BatchSpanProcessor(exporter))
    trace.set_tracer_provider(provider)

    # Auto-instrument FastAPI routes (analogous to AddAspNetCoreInstrumentation)
    FastAPIInstrumentor.instrument_app(
        fastapi_app,
        tracer_provider=provider,
        excluded_urls="/health",  # Don't trace health checks
    )

    # Auto-instrument httpx (covers all Ollama calls via ollama library and direct httpx)
    HTTPXClientInstrumentor().instrument(tracer_provider=provider)

    # Auto-instrument aio-pika (RabbitMQ consumer spans, analogous to AddRabbitMQInstrumentation)
    AioPikaInstrumentor().instrument(tracer_provider=provider)

    # Bridge Python standard logging → OTel so trace context propagates to log records
    LoggingInstrumentor().instrument(set_logging_format=False)

    # Inject trace context into structlog output
    _patch_structlog_with_trace_context()

    _configured = True


def _patch_structlog_with_trace_context() -> None:
    """Add the OTel trace context processor to structlog's processor chain."""
    existing_config = structlog.get_config()
    processors = list(existing_config.get("processors", []))

    # Insert trace context processor right after contextvars merge
    # (so trace_id appears early and is available for all downstream processors)
    insert_at = 1
    processors.insert(insert_at, _otel_trace_context_processor)

    structlog.configure(processors=processors)


def get_tracer(name: str) -> trace.Tracer:
    """Get a named tracer for manual span creation."""
    return trace.get_tracer(name)
