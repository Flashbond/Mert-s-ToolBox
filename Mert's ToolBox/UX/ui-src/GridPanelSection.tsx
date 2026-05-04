import React, { useEffect, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import alternatingIcon from "./Icons/Alternating.svg";
import orientationIcon from "./Icons/Orientation.svg";
import { formatMeters, formatUnits, formatSmart } from "./utils/Formatters";
import { VanillaResolver } from "./utils/VanilliaResolver";
import { parseActiveTool, ActiveTool } from "./utils/ActiveTool";

// --- GLOBAL BINDINGS (C# TO UI) ---
const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None|None");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const gridBlockWidth$ = bindValue<number>("MertsToolBox", "GridBlockWidth");
const gridBlockLength$ = bindValue<number>("MertsToolBox", "GridBlockLength");
const gridColumns$ = bindValue<number>("MertsToolBox", "GridColumns");
const gridRows$ = bindValue<number>("MertsToolBox", "GridRows");

const gridAlternating$ = bindValue<boolean>("MertsToolBox", "GridAlternating");
const gridOrientationLeftBottom$ = bindValue<boolean>("MertsToolBox", "GridOrientationLeftBottom");

const elevationValue$ = bindValue<number>("MertsToolBox", "ElevationValue");
const elevationStepValue$ = bindValue<number>("MertsToolBox", "ElevationStepValue");
const elevationStepArray$ = bindValue<number[]>("MertsToolBox", "ElevationStepArray");

const showSnapRow$ = bindValue<boolean>("MertsToolBox", "ShowSnapRow", true);
const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isSnapNetSideActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetSideActive");
const isSnapNetAreaActive$ = bindValue<boolean>("MertsToolBox", "IsSnapNetAreaActive");

const gridIsOneWaySupported$ = bindValue<boolean>("MertsToolBox", "GridIsOneWaySupported");

// --- COMPONENT DEFINITION ---
export const GridPanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---
    const activeToolRaw = useValue(activeToolMode$) as string;
    const activeTool = parseActiveTool(activeToolRaw);

    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;
    const rawShow: boolean = isToolBoxAllowed && activeTool.id === "Grid";
    const [delayedShow, setDelayedShow] = useState(false);

    useEffect(() => {
        let timeoutId: ReturnType<typeof setTimeout> | undefined;

        if (rawShow) {
            setDelayedShow(true);
        } else {
            timeoutId = setTimeout(() => {
                setDelayedShow(false);
            }, 150);
        }

        return () => {
            if (timeoutId) clearTimeout(timeoutId);
        };
    }, [rawShow]);

    // --- DATA BINDING EVALUATION ---
    const blockWidth = useValue(gridBlockWidth$) as number;
    const blockLength = useValue(gridBlockLength$) as number;
    const columns = useValue(gridColumns$) as number;
    const rows = useValue(gridRows$) as number;

    const isAlternating = useValue(gridAlternating$) as boolean;
    const isOrientationLeftBottom = useValue(gridOrientationLeftBottom$) as boolean;
    const isOneWaySupported = useValue(gridIsOneWaySupported$) as boolean;

    const elevationValue = useValue(elevationValue$) as number;
    const elevationStepValue = useValue(elevationStepValue$) as number;
    const elevationStepValues = useValue(elevationStepArray$) as number[];

    const showSnapRowBinding = useValue(showSnapRow$) as boolean;
    const showSnapRow: boolean = showSnapRowBinding ?? true;

    const isSnapGeometryActive = useValue(isSnapGeometryActive$) as boolean;
    const isSnapNetSideActive = useValue(isSnapNetSideActive$) as boolean;
    const isSnapNetAreaActive = useValue(isSnapNetAreaActive$) as boolean;

    // --- RENDER ---
    if (!delayedShow) return null;

    return (
        <div
            className={`grid-panel-container`}
            onMouseDown={(e) => { e.stopPropagation(); }}
            onContextMenu={(e) => { e.stopPropagation(); }}
            style={{ display: "flex", flexDirection: "column" }}
        >
            <h3 className={'panel-header'} style={{
                paddingLeft: "12rem"
            }}>{activeTool.name}</h3>

            {/* BLOCK WIDTH ROW */}
            <VanillaResolver.instance.Section title="Block Width">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridBlockWidthDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatUnits(blockWidth)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridBlockWidthUp")}
                />
            </VanillaResolver.instance.Section>

            {/* BLOCK DEPTH ROW */}
            <VanillaResolver.instance.Section title="Block Length">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridBlockLengthDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatUnits(blockLength)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridBlockLengthUp")}
                />
            </VanillaResolver.instance.Section>

            {/* COLUMNS ROW */}
            <VanillaResolver.instance.Section title="Columns">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridColumnsDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatSmart(columns)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridColumnsUp")}
                />
            </VanillaResolver.instance.Section>

            {/* ROWS ROW */}
            <VanillaResolver.instance.Section title="Rows">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridRowsDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatSmart(rows)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridRowsUp")}
                />
            </VanillaResolver.instance.Section>

            {/* ONE-WAY PATTERN ROW */}
            <VanillaResolver.instance.Section title="Pattern">
                <VanillaResolver.instance.ToolButton
                    src={alternatingIcon}
                    selected={isAlternating}
                    disabled={!isOneWaySupported}
                    tooltip={isOneWaySupported ? "Alternating" : "REQUIRES ONE-WAY ROAD"}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridToggleAlternating")}
                />

                <VanillaResolver.instance.ToolButton
                    src={orientationIcon}
                    selected={isOrientationLeftBottom}
                    disabled={!isOneWaySupported}
                    tooltip={isOneWaySupported ? "Orientation" : "REQUIRES ONE-WAY ROAD"}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridToggleOrientation")}
                />
            </VanillaResolver.instance.Section>

            {/* ELEVATION ROW */}
            <VanillaResolver.instance.Section title="Elevation">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ElevationDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatMeters(elevationValue)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ElevationUp")}
                />

                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    tooltip={`${elevationStepValue}`}
                    values={elevationStepValues}
                    selectedValue={elevationStepValue}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "ElevationStep", val);
                    }}
                />
            </VanillaResolver.instance.Section>

            {/* SNAP ROW */}
            {showSnapRow && (
                <VanillaResolver.instance.Section title="Snap">
                    <VanillaResolver.instance.ToolButton
                        src="Media/Tools/Snap Options/ExistingGeometry.svg"
                        selected={isSnapGeometryActive}
                        focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                        onSelect={() => trigger("MertsToolBox", "GridToggleSnap", "Geometry")}
                        tooltip={`Existing Geometry`}
                    />

                    <VanillaResolver.instance.ToolButton
                        src="Media/Tools/Snap Options/NetSide.svg"
                        selected={isSnapNetSideActive}
                        focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                        onSelect={() => trigger("MertsToolBox", "GridToggleSnap", "NetSide")}
                        tooltip={`Net Side`}
                    />

                    <VanillaResolver.instance.ToolButton
                        src="Media/Tools/Snap Options/NetArea.svg"
                        selected={isSnapNetAreaActive}
                        focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                        onSelect={() => trigger("MertsToolBox", "GridToggleSnap", "NetArea")}
                        tooltip={`Net Area`}
                    />
                </VanillaResolver.instance.Section>
            )}
        </div>
    );
};