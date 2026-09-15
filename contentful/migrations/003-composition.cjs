module.exports = function (migration) {
  const page = migration
    .createContentType("page")
    .name("Page")
    .description("A URL on the site. Composed by referencing section entries in order.")
    .displayField("title");
  page.createField("title").name("Title").type("Symbol").required(true);
  page.createField("slug").name("Slug").type("Symbol")
    .validations([
      { unique: true },
      {
        regexp: { pattern: "^$|^[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9_]+(?:-[a-z0-9]+)*)*$" },
        message: "Lowercase letters, numbers, hyphens and slashes only. Leave empty for the homepage.",
      },
    ]);
  page.createField("pageType").name("Page type").type("Symbol").required(true)
    .validations([{ in: ["marketing", "dataDriven"] }]);
  page.createField("seo").name("SEO").type("Link").linkType("Entry").required(true)
    .validations([{ linkContentType: ["seo"] }]);
  page.createField("sections").name("Sections").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [] });
  page.createField("breadcrumbParent").name("Breadcrumb parent").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["page"] }]);

  page.changeFieldControl("slug", "builtin", "slugEditor", {
    helpText: "The live URL path. Changing this on a published page breaks existing links — see the redirect note in the README.",
  });

  const article = migration.createContentType("article").name("Article").displayField("title");
  article.createField("title").name("Title").type("Symbol").required(true);
  article.createField("slug").name("Slug").type("Symbol").required(true)
    .validations([
      { unique: true },
      { regexp: { pattern: "^[a-z0-9]+(?:-[a-z0-9]+)*$" }, message: "Lowercase letters, numbers and hyphens only." },
    ]);
  article.createField("seo").name("SEO").type("Link").linkType("Entry").required(true)
    .validations([{ linkContentType: ["seo"] }]);
  article.createField("excerpt").name("Excerpt").type("Text")
    .validations([{ size: { max: 300 } }]);
  article.createField("body").name("Body").type("RichText").required(true);
  article.createField("featuredImage").name("Featured image").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["mediaImage"] }]);
  article.createField("author").name("Author").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["author"] }]);
  article.createField("category").name("Category").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["category"] }]);
  article.createField("tags").name("Tags").type("Array").items({ type: "Symbol" });
  article.createField("publishDate").name("Publish date").type("Date").required(true);
  article.createField("updatedDate").name("Last updated").type("Date");
  article.createField("relatedArticles").name("Related articles").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["article"] }] });

  const navItem = migration.createContentType("navigationItem").name("Navigation Item").displayField("label");
  navItem.createField("label").name("Label").type("Symbol").required(true);
  navItem.createField("page").name("Links to page").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["page", "article"] }]);
  navItem.createField("externalUrl").name("External URL").type("Symbol");
  navItem.createField("children").name("Child items").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["navigationItem"] }] });

  const nav = migration.createContentType("navigation").name("Navigation").displayField("name");
  nav.createField("name").name("Name").type("Symbol").required(true);
  nav.createField("key").name("Key").type("Symbol").required(true)
    .validations([{ unique: true }, { in: ["header", "footer"] }]);
  nav.createField("items").name("Items").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["navigationItem"] }] });
};