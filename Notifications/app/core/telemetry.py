"""OpenTelemetry setup mirroring GraphRAG service."""

from opentelemetry import trace
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.aio_pika import AioPikaInstrumentor
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor
from opentelemetry.instrumentation.logging import LoggingInstrumentor
from opentelemetry.sdk.resources import SERVICE_NAME, Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
import structlog

from app.core.settings import get_settings

_configured = False


def _otel_trace_context_processor(_logger, _method_name, event_dict: dict) -> dict:
    span = trace.get_current_span()
    if span and span.is_recording():
        ctx = span.get_span_context()
        event_dict["trace_id"] = format(ctx.trace_id, "032x")
        event_dict["span_id"] = format(ctx.span_id, "016x")
    return event_dict


def configure_telemetry(fastapi_app=None) -> None:
    global _configured
    if _configured:
        return

    settings = get_settings()
    if not settings.otel_enabled:
        return

    resource = Resource.create({SERVICE_NAME: settings.service_name})
    exporter = OTLPSpanExporter(endpoint=settings.otel_endpoint, insecure=True)
    provider = TracerProvider(resource=resource)
    provider.add_span_processor(BatchSpanProcessor(exporter))
    trace.set_tracer_provider(provider)

    if fastapi_app is not None:
        FastAPIInstrumentor.instrument_app(
            fastapi_app,
            tracer_provider=provider,
            excluded_urls="/health",
        )
    HTTPXClientInstrumentor().instrument(tracer_provider=provider)
    AioPikaInstrumentor().instrument(tracer_provider=provider)
    LoggingInstrumentor().instrument(set_logging_format=False)
    _patch_structlog_with_trace_context()
    _configured = True


def _patch_structlog_with_trace_context() -> None:
    existing_config = structlog.get_config()
    processors = list(existing_config.get("processors", []))
    processors.insert(1, _otel_trace_context_processor)
    structlog.configure(processors=processors)


def get_tracer(name: str) -> trace.Tracer:
    return trace.get_tracer(name)
