const SECTION_TYPES = [
  "sectionHero",
  "sectionRichText",
  "sectionCardGrid",
  "sectionCtaBanner",
  "sectionArticleTeaserList",
  "sectionFundListWidget",
  "sectionFundDetailWidget",
  "sectionFaqAccordion",
  "sectionImageWithText",
  "sectionDisclaimer",
];

module.exports = function (migration) {
  const page = migration.editContentType("page");
  page.editField("sections").items({
    type: "Link",
    linkType: "Entry",
    validations: [
      {
        linkContentType: SECTION_TYPES,
        message: "Only section blocks can be added to a page.",
      },
    ],
  });

  page.changeFieldControl("sections", "builtin", "entryLinksEditor", {
    bulkEditing: false,
    showLinkEntityAction: true,
    showCreateEntityAction: true,
    helpText: "Add, drag to reorder, or remove blocks to compose this page. No developer involvement required.",
  });
};
