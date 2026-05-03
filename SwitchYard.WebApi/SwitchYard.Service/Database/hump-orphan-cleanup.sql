-- One-time cleanup for orphaned hump records whose InstanceID is missing,
-- blank, or no longer exists in humpinstance.

-- Optional preview:
SELECT 'slopeline' AS TableName, COUNT(*) AS OrphanCount FROM slopeline
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = slopeline.InstanceID
)
UNION ALL
SELECT 'position', COUNT(*) FROM position
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = position.InstanceID
)
UNION ALL
SELECT 'positionsegment', COUNT(*) FROM positionsegment
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = positionsegment.InstanceID
)
UNION ALL
SELECT 'switch', COUNT(*) FROM switch
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = switch.InstanceID
)
UNION ALL
SELECT 'retarder', COUNT(*) FROM retarder
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = retarder.InstanceID
)
UNION ALL
SELECT 'wagonconcept', COUNT(*) FROM wagonconcept
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = wagonconcept.InstanceID
)
UNION ALL
SELECT 'operationcondition', COUNT(*) FROM operationcondition
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = operationcondition.InstanceID
)
UNION ALL
SELECT 'humpscheme', COUNT(*) FROM humpscheme
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = humpscheme.InstanceID
)
UNION ALL
SELECT 'vposition', COUNT(*) FROM vposition
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = vposition.InstanceID
)
UNION ALL
SELECT 'vpositionsegment', COUNT(*) FROM vpositionsegment
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = vpositionsegment.InstanceID
)
UNION ALL
SELECT 'humpcalculation', COUNT(*) FROM humpcalculation
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = humpcalculation.InstanceID
)
UNION ALL
SELECT 'humpcalculationdata', COUNT(*) FROM humpcalculationdata
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = humpcalculationdata.InstanceID
)
UNION ALL
SELECT 'retarderstatus', COUNT(*) FROM retarderstatus
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = retarderstatus.InstanceID
)
UNION ALL
SELECT 'headwaycheckscheme', COUNT(*) FROM headwaycheckscheme
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = headwaycheckscheme.InstanceID
)
UNION ALL
SELECT 'headwaycheckwagon', COUNT(*) FROM headwaycheckwagon
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = headwaycheckwagon.InstanceID
)
UNION ALL
SELECT 'headwaycheckdata', COUNT(*) FROM headwaycheckdata
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = headwaycheckdata.InstanceID
)
UNION ALL
SELECT 'headwaycheckresult', COUNT(*) FROM headwaycheckresult
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = headwaycheckresult.InstanceID
);

-- Delete in dependency order.
DELETE FROM headwaycheckdata
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = headwaycheckdata.InstanceID
);

DELETE FROM headwaycheckresult
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = headwaycheckresult.InstanceID
);

DELETE FROM headwaycheckwagon
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = headwaycheckwagon.InstanceID
);

DELETE FROM headwaycheckscheme
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = headwaycheckscheme.InstanceID
);

DELETE FROM retarderstatus
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = retarderstatus.InstanceID
);

DELETE FROM humpcalculationdata
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = humpcalculationdata.InstanceID
);

DELETE FROM humpcalculation
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = humpcalculation.InstanceID
);

DELETE FROM vpositionsegment
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = vpositionsegment.InstanceID
);

DELETE FROM vposition
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = vposition.InstanceID
);

DELETE FROM humpscheme
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = humpscheme.InstanceID
);

DELETE FROM retarder
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = retarder.InstanceID
);

DELETE FROM switch
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = switch.InstanceID
);

DELETE FROM positionsegment
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = positionsegment.InstanceID
);

DELETE FROM position
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = position.InstanceID
);

DELETE FROM slopeline
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = slopeline.InstanceID
);

DELETE FROM wagonconcept
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = wagonconcept.InstanceID
);

DELETE FROM operationcondition
WHERE InstanceID IS NULL OR TRIM(InstanceID) = '' OR NOT EXISTS (
    SELECT 1 FROM humpinstance WHERE humpinstance.ID = operationcondition.InstanceID
);
