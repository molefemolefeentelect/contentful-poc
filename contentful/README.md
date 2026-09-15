# Contentful Content Model Tooling

This directory contains Contentful content-model migrations and a seed script for setting up the CMS.

## Setup

1. Copy `.env.example` to `.env` and fill in your Contentful space ID and API tokens:

```bash
cp .env.example .env
```

Then edit `.env` with your Contentful credentials:
- `CONTENTFUL_SPACE_ID`: Your Contentful space ID
- `CONTENTFUL_MANAGEMENT_TOKEN`: Management API token for applying migrations
- `CONTENTFUL_DELIVERY_TOKEN`: Delivery API token (if needed)
- `CONTENTFUL_PREVIEW_TOKEN`: Preview API token (if needed)
- `CONTENTFUL_ENVIRONMENT`: Target environment (defaults to "master")

## Running Migrations

To apply all migrations to your Contentful space:

```bash
npm run migrate
```

This will execute all `.cjs` migration files in the `migrations/` directory in sorted order.

## Seeding Data

To populate the Contentful space with seed data (requires migrations to have run first):

```bash
npm run seed
```

This runs the seed script located at `seed/seed.mjs`.

## Migration Files

Migration files live in the `migrations/` directory and must end with `.cjs`. They are executed in alphabetical order, so use a numeric prefix for ordering (e.g., `001-initial-setup.cjs`, `002-add-fields.cjs`).

## Seed Fixtures

Seed fixture data can be organized in the `seed/fixtures/` directory.
