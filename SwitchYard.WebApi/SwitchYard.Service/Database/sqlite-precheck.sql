SELECT 'position.ID duplicate', ID, COUNT(*) AS duplicate_count
FROM position
GROUP BY ID
HAVING COUNT(*) > 1;

SELECT 'positionsegment.ID duplicate', ID, COUNT(*) AS duplicate_count
FROM positionsegment
GROUP BY ID
HAVING COUNT(*) > 1;

SELECT 'switch.ID duplicate', ID, COUNT(*) AS duplicate_count
FROM switch
GROUP BY ID
HAVING COUNT(*) > 1;

SELECT 'retarder.ID duplicate', ID, COUNT(*) AS duplicate_count
FROM retarder
GROUP BY ID
HAVING COUNT(*) > 1;

SELECT 'slopeline orphan', s.ID, s.InstanceID
FROM slopeline s
LEFT JOIN humpinstance h ON s.InstanceID = h.ID
WHERE s.InstanceID IS NOT NULL AND h.ID IS NULL;

SELECT 'operationcondition orphan', o.ID, o.InstanceID
FROM operationcondition o
LEFT JOIN humpinstance h ON o.InstanceID = h.ID
WHERE o.InstanceID IS NOT NULL AND h.ID IS NULL;

SELECT 'humpscheme orphan', hs.ID, hs.InstanceID
FROM humpscheme hs
LEFT JOIN humpinstance h ON hs.InstanceID = h.ID
WHERE hs.InstanceID IS NOT NULL AND h.ID IS NULL;

SELECT 'position orphan', p.ID, p.InstanceID, p.SlopeLineID
FROM position p
LEFT JOIN slopeline s ON p.SlopeLineID = s.ID AND p.InstanceID = s.InstanceID
WHERE p.SlopeLineID IS NOT NULL AND s.ID IS NULL;

SELECT 'switch-positionsegment orphan', sw.ID, sw.InstanceID, sw.SlopeLineID, sw.BindingPositionSegmentID
FROM switch sw
LEFT JOIN positionsegment ps
    ON sw.BindingPositionSegmentID = ps.ID
   AND sw.InstanceID = ps.InstanceID
   AND sw.SlopeLineID = ps.SlopeLineID
WHERE sw.BindingPositionSegmentID IS NOT NULL AND ps.ID IS NULL;

SELECT 'headwaycheckscheme orphan', hcs.ID, hcs.InstanceID, hcs.HumpSchemeID
FROM headwaycheckscheme hcs
LEFT JOIN humpscheme hs
    ON hcs.HumpSchemeID = hs.ID
   AND hcs.InstanceID = hs.InstanceID
WHERE hcs.HumpSchemeID IS NOT NULL AND hs.ID IS NULL;
