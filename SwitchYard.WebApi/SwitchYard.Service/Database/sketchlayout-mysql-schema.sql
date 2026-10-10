CREATE TABLE IF NOT EXISTS sketchlayoutscheme (
    Id VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
    OwnerId VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    OwnerName VARCHAR(255) NOT NULL,
    Name VARCHAR(120) NOT NULL,
    Description TEXT NOT NULL,
    Revision INT NOT NULL DEFAULT 1,
    CreatedAt VARCHAR(40) NOT NULL,
    UpdatedAt VARCHAR(40) NOT NULL,
    DocumentJson LONGTEXT NOT NULL,
    INDEX ix_sketchlayoutscheme_owner (OwnerId,UpdatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
