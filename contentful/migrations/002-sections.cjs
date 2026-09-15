const IMAGE = [{ linkContentType: ["mediaImage"] }];

module.exports = function (migration) {
  const card = migration.createContentType("card").name("Card").displayField("title");
  card.createField("title").name("Title").type("Symbol").required(true);
  card.createField("body").name("Body").type("Text");
  card.createField("image").name("Image").type("Link").linkType("Entry").validations(IMAGE);
  card.createField("linkLabel").name("Link label").type("Symbol");
  card.createField("linkUrl").name("Link URL").type("Symbol");
  card.createField("icon").name("Icon name").type("Symbol");

  const faqItem = migration.createContentType("faqItem").name("FAQ Item").displayField("question");
  faqItem.createField("question").name("Question").type("Symbol").required(true);
  faqItem.createField("answer").name("Answer").type("RichText").required(true);

  const hero = migration.createContentType("sectionHero").name("Section: Hero").displayField("internalName");
  hero.createField("internalName").name("Internal name").type("Symbol").required(true);
  hero.createField("eyebrow").name("Eyebrow").type("Symbol");
  hero.createField("heading").name("Heading").type("Symbol").required(true);
  hero.createField("subheading").name("Subheading").type("Text");
  hero.createField("backgroundImage").name("Background image").type("Link").linkType("Entry").validations(IMAGE);
  hero.createField("primaryCtaLabel").name("Primary CTA label").type("Symbol");
  hero.createField("primaryCtaUrl").name("Primary CTA URL").type("Symbol");
  hero.createField("secondaryCtaLabel").name("Secondary CTA label").type("Symbol");
  hero.createField("secondaryCtaUrl").name("Secondary CTA URL").type("Symbol");
  hero.createField("variant").name("Variant").type("Symbol")
    .validations([{ in: ["default", "compact", "imageRight"] }]);

  const richText = migration.createContentType("sectionRichText").name("Section: Rich Text").displayField("internalName");
  richText.createField("internalName").name("Internal name").type("Symbol").required(true);
  richText.createField("heading").name("Heading").type("Symbol");
  richText.createField("body").name("Body").type("RichText").required(true);
  richText.createField("width").name("Width").type("Symbol")
    .validations([{ in: ["narrow", "default", "wide"] }]);

  const cardGrid = migration.createContentType("sectionCardGrid").name("Section: Card Grid").displayField("internalName");
  cardGrid.createField("internalName").name("Internal name").type("Symbol").required(true);
  cardGrid.createField("heading").name("Heading").type("Symbol");
  cardGrid.createField("intro").name("Intro").type("Text");
  cardGrid.createField("cards").name("Cards").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["card"] }] });
  cardGrid.createField("columns").name("Columns").type("Integer")
    .validations([{ range: { min: 2, max: 4 } }]);

  const cta = migration.createContentType("sectionCtaBanner").name("Section: CTA Banner").displayField("internalName");
  cta.createField("internalName").name("Internal name").type("Symbol").required(true);
  cta.createField("heading").name("Heading").type("Symbol").required(true);
  cta.createField("body").name("Body").type("Text");
  cta.createField("ctaLabel").name("CTA label").type("Symbol").required(true);
  cta.createField("ctaUrl").name("CTA URL").type("Symbol").required(true);
  cta.createField("variant").name("Variant").type("Symbol")
    .validations([{ in: ["primary", "muted", "accent"] }]);

  const teasers = migration.createContentType("sectionArticleTeaserList").name("Section: Article Teaser List").displayField("internalName");
  teasers.createField("internalName").name("Internal name").type("Symbol").required(true);
  teasers.createField("heading").name("Heading").type("Symbol");
  teasers.createField("mode").name("Mode").type("Symbol").required(true)
    .validations([{ in: ["latest", "manual"] }]);
  teasers.createField("category").name("Category filter").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["category"] }]);
  teasers.createField("limit").name("Number of articles").type("Integer")
    .validations([{ range: { min: 1, max: 12 } }]);
  teasers.createField("articles").name("Manually chosen articles").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["article"] }] });

  const fundList = migration.createContentType("sectionFundListWidget").name("Section: Fund List Widget").displayField("internalName");
  fundList.createField("internalName").name("Internal name").type("Symbol").required(true);
  fundList.createField("heading").name("Heading").type("Symbol");
  fundList.createField("intro").name("Intro").type("Text");
  fundList.createField("categoryId").name("Fund category id").type("Symbol");
  fundList.createField("topN").name("Show top N").type("Integer")
    .validations([{ range: { min: 1, max: 50 } }]);
  fundList.createField("displayVariant").name("Display variant").type("Symbol")
    .validations([{ in: ["grid", "table", "compact"] }]);
  fundList.createField("showFilters").name("Show category filters").type("Boolean").defaultValue({ "en-US": false });

  const fundDetail = migration.createContentType("sectionFundDetailWidget").name("Section: Fund Detail Widget").displayField("internalName");
  fundDetail.createField("internalName").name("Internal name").type("Symbol").required(true);
  fundDetail.createField("fundCode").name("Fund code").type("Symbol")
    .validations([]);
  fundDetail.createField("showPerformance").name("Show performance").type("Boolean").defaultValue({ "en-US": false });
  fundDetail.createField("showPriceHistory").name("Show price history").type("Boolean").defaultValue({ "en-US": false });
  fundDetail.createField("showFactsheet").name("Show factsheet link").type("Boolean").defaultValue({ "en-US": false });

  const faq = migration.createContentType("sectionFaqAccordion").name("Section: FAQ Accordion").displayField("internalName");
  faq.createField("internalName").name("Internal name").type("Symbol").required(true);
  faq.createField("heading").name("Heading").type("Symbol");
  faq.createField("items").name("Questions").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["faqItem"] }] });
  faq.createField("emitFaqSchema").name("Emit FAQPage structured data").type("Boolean").defaultValue({ "en-US": false });

  const imageText = migration.createContentType("sectionImageWithText").name("Section: Image With Text").displayField("internalName");
  imageText.createField("internalName").name("Internal name").type("Symbol").required(true);
  imageText.createField("heading").name("Heading").type("Symbol");
  imageText.createField("body").name("Body").type("RichText");
  imageText.createField("image").name("Image").type("Link").linkType("Entry").validations(IMAGE);
  imageText.createField("imagePosition").name("Image position").type("Symbol")
    .validations([{ in: ["left", "right"] }]);
  imageText.createField("ctaLabel").name("CTA label").type("Symbol");
  imageText.createField("ctaUrl").name("CTA URL").type("Symbol");

  const disclaimer = migration.createContentType("sectionDisclaimer").name("Section: Disclaimer").displayField("internalName");
  disclaimer.createField("internalName").name("Internal name").type("Symbol").required(true);
  disclaimer.createField("label").name("Label").type("Symbol");
  disclaimer.createField("body").name("Body").type("RichText").required(true);
  disclaimer.createField("severity").name("Severity").type("Symbol")
    .validations([{ in: ["info", "warning"] }]);
  disclaimer.createField("collapsible").name("Collapsible").type("Boolean").defaultValue({ "en-US": false });
};
