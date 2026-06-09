from pathlib import Path

from jinja2 import Environment, FileSystemLoader, Undefined, select_autoescape

from app.delivery.base import RenderedTemplate

TEMPLATES_DIR = Path(__file__).resolve().parent.parent / "templates"


class _SilentUndefined(Undefined):
    def __str__(self) -> str:
        return ""

    def __iter__(self):
        return iter([])

    def __bool__(self) -> bool:
        return False


class TemplateRenderer:
    def __init__(self, templates_dir: Path | None = None) -> None:
        base = templates_dir or TEMPLATES_DIR
        self._env = Environment(
            loader=FileSystemLoader(str(base)),
            autoescape=select_autoescape(["html", "xml"]),
            undefined=_SilentUndefined,
        )

    def render(self, slug: str, context: dict) -> RenderedTemplate:
        safe_context = {k: (v if v is not None else "") for k, v in context.items()}
        subject = self._env.get_template(f"{slug}/subject.j2").render(**safe_context).strip()
        body_html = self._env.get_template(f"{slug}/body.html.j2").render(**safe_context)
        try:
            body_text = self._env.get_template(f"{slug}/body.txt.j2").render(**safe_context)
        except Exception:
            body_text = ""
        return RenderedTemplate(subject=subject, body_html=body_html, body_text=body_text)
