-- Run once as a database administrator before enabling SketchLayoutDatabase in production.
-- Grant the service account access to sketchlayout.* using the deployment's existing account.
-- Tables are initialized by DatabaseSchemaInitializer on service startup.
CREATE DATABASE IF NOT EXISTS sketchlayout CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
