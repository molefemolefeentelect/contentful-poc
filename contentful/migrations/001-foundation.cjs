module.exports = function (migration) {
  const mediaImage = migration
    .createContentType("mediaImage")
    .name("Media Image")
    .description("An image plus mandatory alt text. Referenced instead of raw Assets so alt text can be enforced.")
    .displayField("internalName");
  mediaImage.createField("internalName").name("Internal name").type("Symbol").required(true);
  mediaImage.createField("image").name("Image").type("Link").linkType("Asset").required(true);
  mediaImage.createField("altText").name("Alt text").type("Symbol").required(true)
    .validations([{ size: { max: 125 }, message: "Alt text must be 125 characters or fewer." }]);
  mediaImage.createField("caption").name("Caption").type("Symbol");

  const seo = migration
    .createContentType("seo")
    .name("SEO")
    .description("Reusable SEO metadata. Referenced by every page-like content type.")
    .displayField("internalName");
  seo.createField("internalName").name("Internal name").type("Symbol").required(true);
  seo.createField("metaTitle").name("Meta title").type("Symbol").required(true)
    .validations([{ size: { max: 60 }, message: "Keep meta titles to 60 characters so they are not truncated in search results." }]);
  seo.createField("metaDescription").name("Meta description").type("Text").required(true)
    .validations([{ size: { max: 160 }, message: "Keep meta descriptions to 160 characters so they are not truncated in search results." }]);
  seo.createField("ogTitle").name("Open Graph title override").type("Symbol");
  seo.createField("ogDescription").name("Open Graph description override").type("Text");
  seo.createField("ogImage").name("Open Graph image").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["mediaImage"] }]);
  seo.createField("canonicalUrl").name("Canonical URL override").type("Symbol")
    .validations([{ regexp: { pattern: "^https?://.+" }, message: "Must be an absolute URL starting with http:// or https://" }]);
  seo.createField("noindex").name("Hide from search engines (noindex)").type("Boolean")
    .defaultValue({ "en-US": false });
  seo.createField("nofollow").name("Do not follow links (nofollow)").type("Boolean")
    .defaultValue({ "en-US": false });
  seo.createField("structuredDataType").name("Structured data type").type("Symbol")
    .validations([{ in: ["WebPage", "Organization", "Article", "Product", "FAQPage", "BreadcrumbList", "None"] }]);
  seo.createField("structuredDataOverrides").name("Structured data overrides").type("Object")
    .validations([]);

  seo.changeFieldControl("noindex", "builtin", "boolean", { trueLabel: "Noindex", falseLabel: "Indexable" });
  seo.changeFieldControl("nofollow", "builtin", "boolean", { trueLabel: "Nofollow", falseLabel: "Follow" });
  seo.changeFieldControl("structuredDataType", "builtin", "dropdown");

  const category = migration.createContentType("category").name("Category").displayField("name");
  category.createField("name").name("Name").type("Symbol").required(true);
  category.createField("slug").name("Slug").type("Symbol").required(true)
    .validations([{ unique: true }, { regexp: { pattern: "^[a-z0-9]+(?:-[a-z0-9]+)*$" }, message: "Lowercase letters, numbers and hyphens only." }]);

  const author = migration.createContentType("author").name("Author").displayField("name");
  author.createField("name").name("Name").type("Symbol").required(true);
  author.createField("jobTitle").name("Job title").type("Symbol");
  author.createField("bio").name("Bio").type("Text");
  author.createField("photo").name("Photo").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["mediaImage"] }]);

  const settings = migration.createContentType("siteSettings").name("Site Settings").displayField("siteName");
  settings.createField("siteName").name("Site name").type("Symbol").required(true);
  settings.createField("logo").name("Logo").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["mediaImage"] }]);
  settings.createField("defaultSeo").name("Default SEO fallback").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["seo"] }]);
  settings.createField("socialLinks").name("Social links").type("Object");
  settings.createField("disclaimerText").name("Global disclaimer").type("RichText");
  settings.createField("legalFooterLinks").name("Legal footer links").type("Object");
  settings.createField("organizationLegalName").name("Organisation legal name").type("Symbol");
  settings.createField("organizationSameAs").name("Organisation sameAs URLs").type("Array")
    .items({ type: "Symbol" });
};
