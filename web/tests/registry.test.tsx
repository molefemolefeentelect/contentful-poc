import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SectionRenderer } from "@/components/sections/SectionRenderer";
import type { Section } from "@/lib/types";

describe("SectionRenderer", () => {
  it("renders each known section type through the registry", () => {
    const sections = [
      { __type: "sectionHero", id: "h1", heading: "Own the market", variant: "default" },
      { __type: "sectionCtaBanner", id: "c1", heading: "Start investing", cta: { label: "Invest now", url: "/invest" }, variant: "primary" },
    ] as unknown as Section[];

    render(<SectionRenderer sections={sections} />);

    expect(screen.getByText("Own the market")).toBeDefined();
    expect(screen.getByText("Invest now")).toBeDefined();
  });

  it("skips an unknown section type without crashing the page", () => {
    const sections = [
      { __type: "sectionFutureThing", id: "x1" },
      { __type: "sectionHero", id: "h1", heading: "Still rendered", variant: "default" },
    ] as unknown as Section[];

    render(<SectionRenderer sections={sections} />);

    expect(screen.getByText("Still rendered")).toBeDefined();
  });

  it("preserves the order the admin user set in Contentful", () => {
    const sections = [
      { __type: "sectionHero", id: "h1", heading: "First", variant: "default" },
      { __type: "sectionHero", id: "h2", heading: "Second", variant: "default" },
    ] as unknown as Section[];

    const { container } = render(<SectionRenderer sections={sections} />);
    const headings = Array.from(container.querySelectorAll("h1, h2")).map((n) => n.textContent);

    expect(headings).toEqual(["First", "Second"]);
  });
});
