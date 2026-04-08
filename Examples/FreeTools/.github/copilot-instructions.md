# Copilot Instructions

## Project Guidelines
- User's preferred API pattern: three endpoints per entity — GetMany (null/empty → all, list of IDs → filtered), SaveMany (array of objects, PK exists → update, empty/new PK → insert), DeleteMany (must provide IDs, null/empty → error). Single-item convenience methods just wrap the batch versions with single-item lists.