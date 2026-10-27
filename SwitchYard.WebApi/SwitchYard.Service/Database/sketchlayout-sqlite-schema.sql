CREATE TABLE IF NOT EXISTS sketchlayoutscheme (
    Id TEXT PRIMARY KEY NOT NULL,
    OwnerId TEXT NOT NULL,
    OwnerName TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NOT NULL DEFAULT '',
    Revision INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    DocumentJson TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_sketchlayoutscheme_owner ON sketchlayoutscheme(OwnerId,UpdatedAt);
