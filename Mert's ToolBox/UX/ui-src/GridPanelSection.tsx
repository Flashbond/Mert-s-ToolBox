import React, { useEffect, useState } from "react";
import { trigger, call, bindValue, useValue } from "cs2/api";
import crossWalkIcon from "./icons/CrossWalk.svg";
import alternatingIcon from "./icons/Alternating.svg";
import orientationIcon from "./icons/Orientation.svg";
import trafficLightIcon from "./icons/TrafficLight.svg";
import flattenIcon from "./icons/Flatten.svg";
import { formatMeters, formatUnits, formatSmart } from "./utils/Formatters";
import { VanillaResolver } from "./utils/VanilliaResolver";
import { parseActiveTool } from "./utils/ActiveTool";
import { MertListBox } from './utils/MertListBox';

// --- GLOBAL BINDINGS (C# TO UI) ---
const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None|None");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const gridBlockWidth$ = bindValue<number>("MertsToolBox", "GridBlockWidth");
const gridBlockLength$ = bindValue<number>("MertsToolBox", "GridBlockLength");
const gridColumns$ = bindValue<number>("MertsToolBox", "GridColumns");
const gridRows$ = bindValue<number>("MertsToolBox", "GridRows");

const gridAlternating$ = bindValue<boolean>("MertsToolBox", "GridAlternating");
const gridOrientationLeftBottom$ = bindValue<boolean>("MertsToolBox", "GridOrientationLeftBottom");

const suppressCrosswalks$ = bindValue<boolean>("MertsToolBox", "SuppressCrosswalks", false);
const suppressTrafficLights$ = bindValue<boolean>("MertsToolBox", "SuppressTrafficLights", false);
const elevationValue$ = bindValue<number>("MertsToolBox", "ElevationValue");
const elevationStepValue$ = bindValue<number>("MertsToolBox", "ElevationStepValue");
const elevationStepArray$ = bindValue<number[]>("MertsToolBox", "ElevationStepArray");

const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isFlattenActive$ = bindValue<boolean>("MertsToolBox", "IsFlattenActive");

const gridIsOneWaySupported$ = bindValue<boolean>("MertsToolBox", "GridIsOneWaySupported");

const presetList$ = bindValue<string>("MertsToolBox", "PresetList", "");


// --- COMPONENT DEFINITION ---
export const GridPanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---
    const activeToolRaw = useValue(activeToolMode$) as string;
    const activeTool = parseActiveTool(activeToolRaw);

    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;
    const rawShow: boolean = isToolBoxAllowed && activeTool.id === "Grid";
    const [delayedShow, setDelayedShow] = useState(false);

    const [presetPanelOpen, setPresetPanelOpen] = useState(false);

    useEffect(() => {
        let timeoutId: ReturnType<typeof setTimeout> | undefined;

        if (rawShow) {
            setDelayedShow(true);
        } else {
            timeoutId = setTimeout(() => {
                setDelayedShow(false);
                setPresetPanelOpen(false);
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

    const suppressCrosswalks = useValue(suppressCrosswalks$) as boolean;
    const suppressTrafficLights = useValue(suppressTrafficLights$) as boolean;
    const elevationValue = useValue(elevationValue$) as number;
    const elevationStepValue = useValue(elevationStepValue$) as number;
    const elevationStepValues = useValue(elevationStepArray$) as number[];

    const isSnapGeometryActive = useValue(isSnapGeometryActive$) as boolean;
    const isFlattenActive = useValue(isFlattenActive$) as boolean;

    const presetListRaw = useValue(presetList$) as string;

    type PresetListItem = {
        label: string;
        value: string;
    };

    const presetList: PresetListItem[] = (presetListRaw || "")
        .split(";")
        .map((item) => item.trim())
        .filter((item) => item.length > 0)
        .map((item) => {
            const [label, value] = item.split("|");

            return {
                label: label?.trim() ?? item,
                value: value?.trim() ?? label?.trim() ?? item,
            };
        });

    // --- RENDER ---
    if (!delayedShow) return null;

    return (
        <div
            className={`grid-panel-container`}
            onMouseDown={(e) => { e.stopPropagation(); }}
            onContextMenu={(e) => { e.stopPropagation(); }}
            style={{ display: "flex", flexDirection: "column" }}
        >
            <div className={'panel-header'} style={{
                fontSize: "1.1em",
                fontWeight: 600,
                padding: "2rem 10rem"
            }}>{activeTool.name}</div>

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
                    tooltip={
                        !isOneWaySupported
                            ? "REQUIRES ONE-WAY ROAD"
                            : (isAlternating ? "Parallel road directions alternate" : "Parallel roads heads the same direction")
                    }
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridToggleAlternating")}
                />
                <VanillaResolver.instance.ToolButton
                    src={orientationIcon}
                    selected={isOrientationLeftBottom}
                    disabled={!isOneWaySupported}
                    tooltip={
                        !isOneWaySupported
                            ? "REQUIRES ONE-WAY ROAD"
                            : (isOrientationLeftBottom ? "Road order starts with left to bottom" : "Road order starts with right to bottom")
                    }
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "GridToggleOrientation")}
                />
            </VanillaResolver.instance.Section>

            <VanillaResolver.instance.Section title="Remove Traffic Signs">
                <VanillaResolver.instance.ToolButton
                    src={crossWalkIcon}
                    selected={suppressCrosswalks}
                    tooltip={suppressCrosswalks ? "Crosswalks are removed" : "Crosswalks are allowed"}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleSuppressCrosswalks")}
                />
                <VanillaResolver.instance.ToolButton
                    src={trafficLightIcon}
                    selected={suppressTrafficLights}
                    tooltip={suppressTrafficLights ? "Traffic Lights are removed" : "Traffic Lights are allowed"}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleSuppressTrafficLights")}
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
                    onSelect={(val) => trigger("MertsToolBox", "ElevationStep", val)}
                />
            </VanillaResolver.instance.Section>

            {/* SNAP ROW */}
            <VanillaResolver.instance.Section title="Snap">
                <VanillaResolver.instance.ToolButton
                    src="Media/Tools/Snap Options/ExistingGeometry.svg"
                    selected={isSnapGeometryActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleSnap")}
                    tooltip={`Snap to existing geometry`}
                />
            </VanillaResolver.instance.Section>

            {/* FLATTEN ROW */}
            <VanillaResolver.instance.Section title="Flatten">
                <VanillaResolver.instance.ToolButton
                    src={flattenIcon}
                    selected={isFlattenActive}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleFlattenGeometry")}
                    tooltip={isFlattenActive ? "Shape stays flat; ignores terrain" : "Shape follows terrain"}
                />
            </VanillaResolver.instance.Section>

            {/* MERT LISTBOX (KLASİK) */}
            <MertListBox
                items={presetList.map(p => p.label)}
                isOpen={presetPanelOpen}
                onToggleOpen={() => {
                    setPresetPanelOpen(!presetPanelOpen);
                    if (!presetPanelOpen) trigger("MertsToolBox", "RefreshPresetList");
                }}
                onSave={async () => {
                    const success = await call<boolean>("MertsToolBox", "SavePreset", activeTool.id);
                    if (success) {
                        trigger("MertsToolBox", "RefreshPresetList");
                    }
                    return success;
                }}
                onSelect={(presetLabel) => {
                    const preset = presetList.find(p => p.label === presetLabel);
                    if (!preset) return;

                    trigger("MertsToolBox", "LoadPreset", preset.value);
                    setPresetPanelOpen(false);
                }}
                onDelete={(presetLabel) => {
                    const preset = presetList.find(p => p.label === presetLabel);
                    if (!preset) return;

                    trigger("MertsToolBox", "DeletePreset", preset.value);
                }}
            />
        </div>
    );
};