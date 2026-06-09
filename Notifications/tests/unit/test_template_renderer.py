from app.application.template_renderer import TemplateRenderer


def test_template_renderer_uses_safe_defaults_for_missing_fields(templates_dir):
    renderer = TemplateRenderer(templates_dir)
    rendered = renderer.render(
        "order_confirmation",
        {"orderId": "abc-123", "totalAmount": 10, "currency": "EUR"},
    )
    assert "abc-123" in rendered.body_html
    assert rendered.subject


def test_template_renderer_renders_line_summary(templates_dir):
    renderer = TemplateRenderer(templates_dir)
    rendered = renderer.render(
        "order_confirmation",
        {
            "orderId": "abc",
            "lineSummary": "2× Widget",
            "totalAmount": 20,
            "currency": "EUR",
        },
    )
    assert "2× Widget" in rendered.body_html
