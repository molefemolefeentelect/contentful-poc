import type { ComponentType } from "react";
import type { Section } from "@/lib/types";
import { Hero } from "./Hero";
import { RichTextBlock } from "./RichTextBlock";
import { CardGrid } from "./CardGrid";
import { CtaBanner } from "./CtaBanner";
import { ArticleTeaserList } from "./ArticleTeaserList";
import { FundListWidget } from "./FundListWidget";
import { FundDetailWidget } from "./FundDetailWidget";
import { FaqAccordion } from "./FaqAccordion";
import { ImageWithText } from "./ImageWithText";
import { Disclaimer } from "./Disclaimer";

/**
 * The only file a developer touches to support a NEW kind of block.
 * Composing a page from existing blocks requires no code change at all.
 */
export const sectionRegistry: Record<Section["__type"], ComponentType<never>> = {
  sectionHero: Hero,
  sectionRichText: RichTextBlock,
  sectionCardGrid: CardGrid,
  sectionCtaBanner: CtaBanner,
  sectionArticleTeaserList: ArticleTeaserList,
  sectionFundListWidget: FundListWidget,
  sectionFundDetailWidget: FundDetailWidget,
  sectionFaqAccordion: FaqAccordion,
  sectionImageWithText: ImageWithText,
  sectionDisclaimer: Disclaimer,
} as Record<Section["__type"], ComponentType<never>>;
