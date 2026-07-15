CREATE TABLE IF NOT EXISTS "capacityinstance" (
    "ID" TEXT NULL,
    "Name" TEXT NULL,
    "Owner" TEXT NULL,
    "CreatedDate" DATETIME NULL,
    "IsActive" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "stationscheme" (
    "InstanceID" TEXT NULL,
    "ID" TEXT NULL,
    "Name" TEXT NULL,
    "DisplayStyles" TEXT NULL,
    "GridSettings" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "stationrouteend" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "BindingNodeID" TEXT NULL,
    "Type" TEXT NULL,
    "SegmentTag" TEXT NULL,
    "SidingTag" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "stationroute" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "Type" TEXT NULL,
    "Description" TEXT NULL,
    "NodeList" TEXT NULL,
    "LinkList" TEXT NULL,
    "SwitchList" TEXT NULL,
    "CellList" TEXT NULL,
    "InterruptCellList" TEXT NULL,
    "SignalList" TEXT NULL,
    "AllowanceTags" TEXT NULL,
    "ForbiddenTags" TEXT NULL,
    "StartNodeID" TEXT NULL,
    "EndNodeID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "cell" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "LinkIDList" TEXT NULL,
    "Name" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "annotation" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "Text" TEXT NULL,
    "X" REAL NULL,
    "Y" REAL NULL,
    "FontFamily" TEXT NULL,
    "FontSize" REAL NULL,
    "FontWeight" TEXT NULL,
    "FontStyle" TEXT NULL,
    "Angle" REAL NULL,
    "TextColor" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "curve" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "BindingNodeID" TEXT NULL,
    "BindingLink1ID" TEXT NULL,
    "BindingLink2ID" TEXT NULL,
    "Radius" INTEGER NULL,
    "Angle" REAL NULL,
    "TangentDistance" REAL NULL,
    "StartX" REAL NULL,
    "StartY" REAL NULL,
    "EndX" REAL NULL,
    "EndY" REAL NULL,
    "CenterX" REAL NULL,
    "CenterY" REAL NULL,
    "LargeArcFlag" INTEGER NULL,
    "SweepFlag" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "operationplan" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "Name" TEXT NULL,
    "Description" TEXT NULL,
    "SortOrder" INTEGER NULL,
    "CreatedDate" DATETIME NULL,
    "UpdatedDate" DATETIME NULL
);

CREATE TABLE IF NOT EXISTS "traintemplate" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "TrainTemplateID" TEXT NULL,
    "Name" TEXT NULL,
    "Type" TEXT NULL,
    "Number" INTEGER NULL,
    "IsFixedOperation" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "movementtemplate" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "TrainTemplateID" TEXT NULL,
    "MovementID" TEXT NULL,
    "Name" TEXT NULL,
    "RouteIDList" TEXT NULL,
    "MinDuration" INTEGER NULL,
    "SortOrder" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "train" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "ID" TEXT NULL,
    "TrainTemplateID" TEXT NULL,
    "TrainNumber" TEXT NULL,
    "Name" TEXT NULL,
    "TrainType" TEXT NULL,
    "IsFixedOperation" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "movement" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "TrainID" TEXT NULL,
    "TrainTemplateID" TEXT NULL,
    "MovementID" TEXT NULL,
    "Name" TEXT NULL,
    "RouteIDList" TEXT NULL,
    "MinDuration" INTEGER NULL,
    "EarliestStartTime" TEXT NULL,
    "LatestEndTime" TEXT NULL,
    "Route" TEXT NULL,
    "Tag" TEXT NULL,
    "SortOrder" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "operationbottlenecksummarycategory" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "CategoryID" TEXT NULL,
    "Name" TEXT NULL,
    "SortOrder" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "operationbottlenecksummarycategoryroute" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "CategoryID" TEXT NULL,
    "RouteID" TEXT NULL,
    "SortOrder" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "operationanalysismeta" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "TotalTimeSeconds" INTEGER NULL,
    "UpdatedDate" DATETIME NULL
);

CREATE TABLE IF NOT EXISTS "operationanalysiscell" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "CellID" TEXT NULL,
    "CellName" TEXT NULL,
    "SortOrder" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "operationoccupationtimerow" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "RowKey" TEXT NULL,
    "RowType" TEXT NULL,
    "SequenceText" TEXT NULL,
    "RouteID" TEXT NULL,
    "RouteName" TEXT NULL,
    "OperationCountText" TEXT NULL,
    "SortOrder" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "operationoccupationtimecell" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "RowKey" TEXT NULL,
    "CellID" TEXT NULL,
    "CellValue" REAL NULL,
    "InterruptCellValue" REAL NULL
);

CREATE TABLE IF NOT EXISTS "operationoccupationtimesubtable" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "SubTableID" TEXT NULL,
    "SubTableName" TEXT NULL,
    "CellIDList" TEXT NULL,
    "SortOrder" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "operationbottleneckanalysisresult" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "RouteID" TEXT NULL,
    "RouteName" TEXT NULL,
    "OperationCount" INTEGER NULL,
    "BottleneckCellID" TEXT NULL,
    "BottleneckCellName" TEXT NULL,
    "BottleneckUtilization" REAL NULL,
    "ThroughputCapacity" REAL NULL,
    "SortOrder" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "operationthroughputsummaryresult" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "CategoryID" TEXT NULL,
    "GroupKey" TEXT NULL,
    "GroupText" TEXT NULL,
    "RouteCount" INTEGER NULL,
    "OperationCount" INTEGER NULL,
    "CapacityTotal" REAL NULL,
    "CapacityAverage" REAL NULL,
    "SortOrder" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "operationthroughputsummaryroute" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "OperationPlanID" TEXT NULL,
    "CategoryID" TEXT NULL,
    "RouteID" TEXT NULL,
    "SortOrder" INTEGER NULL
);
