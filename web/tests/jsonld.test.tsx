import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { JsonLd } from "@/components/seo/JsonLd";

describe("JsonLd", () => {
  it("emits one ld+json script per schema object", () => {
    const { container } = render(
      <JsonLd data={[{ "@type": "WebPage", name: "Home" }, { "@type": "BreadcrumbList" }]} />,
    );
    expect(container.querySelectorAll('script[type="application/ld+json"]')).toHaveLength(2);
  });

  it("escapes angle brackets so a script tag in content cannot break out", () => {
    const { container } = render(<JsonLd data={[{ name: "</script><img onerror=alert(1)>" }]} />);
    const html = container.querySelector("script")!.innerHTML;

    expect(html).not.toContain("</script>");
    expect(html).toContain("\\u003c");
  });

  it("renders nothing when there is no structured data", () => {
    const { container } = render(<JsonLd data={[]} />);
    expect(container.querySelector("script")).toBeNull();
  });
});
