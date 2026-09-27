import React, { useEffect, useState } from "react";
import { trigger, call, bindValue, useValue } from "cs2/api";
import StraightIcon from "./icons/StraightCorner.svg";
import directionIcon from "./icons/Direction.svg";
import crossWalkIcon from "./icons/CrossWalk.svg";
import flattenIcon from "./icons/Flatten.svg";
import { MertSlider } from "./utils/MertSlider";
import { formatMeters } from "./utils/Formatters";
import { VanillaResolver } from "./utils/VanilliaResolver";
import { parseActiveTool } from "./utils/ActiveTool";
import { MertListBox } from './utils/MertListBox';

// --- GLOBAL BINDINGS (C# TO UI) ---
const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None|None");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const softBlockWidth$ = bindValue<number>("MertsToolBox", "SoftBlockWidth");
const softBlockWidthStepValue$ = bindValue<number>("MertsToolBox", "SoftBlockWidthStepValue");
const softBlockWidthStepArray$ = bindValue<number[]>("MertsToolBox", "SoftBlockWidthStepArray");

const softBlockLength$ = bindValue<number>("MertsToolBox", "SoftBlockLength");
const softBlockLengthStepValue$ = bindValue<number>("MertsToolBox", "SoftBlockLengthStepValue");
const softBlockLengthStepArray$ = bindValue<number[]>("MertsToolBox", "SoftBlockLengthStepArray");

const borderRadius$ = bindValue<number>("MertsToolBox", "GetBorderRadius");

const softBlockStraightCorners$ = bindValue<boolean>("MertsToolBox", "SoftBlockStraightCorners", false);
const softBlockStraightCornersSupported$ = bindValue<boolean>("MertsToolBox", "SoftBlockStraightCornersSupported", true);
const softBlockIsClockwise$ = bindValue<boolean>("MertsToolBox", "MainDirectionState", false);
const softBlockIsOneWaySupported$ = bindValue<boolean>("MertsToolBox", "SoftBlockIsOneWaySupported", false);
const suppressCrosswalks$ = bindValue<boolean>("MertsToolBox", "SuppressCrosswalks", false);

const elevationValue$ = bindValue<number>("MertsToolBox", "ElevationValue");
const elevationStepValue$ = bindValue<number>("MertsToolBox", "ElevationStepValue");
const elevationStepArray$ = bindValue<number[]>("MertsToolBox", "ElevationStepArray");

const isSnapGeometryActive$ = bindValue<boolean>("MertsToolBox", "IsSnapGeometryActive");
const isFlattenActive$ = bindValue<boolean>("MertsToolBox", "IsFlattenActive");

const presetList$ = bindValue<string>("MertsToolBox", "PresetList", "");

// --- COMPONENT DEFINITION ---
export const SoftBlockPanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---
    const activeToolRaw = useValue(activeToolMode$) as string;
    const activeTool = parseActiveTool(activeToolRaw);

    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;
    const rawShow: boolean = isToolBoxAllowed && activeTool.id === "SoftBlock";
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
    const width = useValue(softBlockWidth$) as number;
    const widthStepValue = useValue(softBlockWidthStepValue$) as number;
    const widthStepValues = useValue(softBlockWidthStepArray$) as number[];

    const length = useValue(softBlockLength$) as number;
    const lengthStepValue = useValue(softBlockLengthStepValue$) as number;
    const lengthStepValues = useValue(softBlockLengthStepArray$) as number[];

    const borderRadius = useValue(borderRadius$) as number;

    const straightCorners = useValue(softBlockStraightCorners$) as boolean;
    const straightCornersSupported = useValue(softBlockStraightCornersSupported$) as boolean;
    const isClockwise = useValue(softBlockIsClockwise$) as boolean;
    const isOneWaySupported = useValue(softBlockIsOneWaySupported$) as boolean;
    const suppressCrosswalks = useValue(suppressCrosswalks$) as boolean;

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
            className={`softblock-panel-container`}
            onMouseDown={(e) => { e.stopPropagation(); }}
            onContextMenu={(e) => { e.stopPropagation(); }}
            style={{ display: "flex", flexDirection: "column" }}
        >
            <div className={'panel-header'} style={{
                fontSize: "1.1em",
                fontWeight: 600,
                padding: "2rem 10rem"
            }}>{activeTool.name}</div>

            {/* WIDTH ROW */}
            <VanillaResolver.instance.Section title="Width">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SoftBlockWidthDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatMeters(width)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SoftBlockWidthUp")}
                />
                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={widthStepValue}
                    values={widthStepValues}
                    tooltip={`${widthStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "SoftBlockWidthStep", val);
                    }}
                />
            </VanillaResolver.instance.Section>

            {/* LENGTH ROW */}
            <VanillaResolver.instance.Section title="Length">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SoftBlockLengthDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>
                    {formatMeters(length)}
                </div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SoftBlockLengthUp")}
                />
                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={lengthStepValue}
                    values={lengthStepValues}
                    tooltip={`${lengthStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "SoftBlockLengthStep", val);
                    }}
                />
            </VanillaResolver.instance.Section>

            {/* BORDER RADIUS ROW */}
            <VanillaResolver.instance.Section title="Border Radius">
                <MertSlider
                    min={1}
                    max={10}
                    step={0.1}
                    value={borderRadius}
                    onDragStart={() => {
                        trigger("MertsToolBox", "BeginBorderRadiusDrag");
                    }}
                    onDragEnd={() => {
                        trigger("MertsToolBox", "EndBorderRadiusDrag");
                    }}
                    onChange={(newVal) => {
                        trigger("MertsToolBox", "SetBorderRadius", newVal);
                    }}
                    formatValue={(v) => v.toFixed(1)}
                />
            </VanillaResolver.instance.Section>

            <VanillaResolver.instance.Section title="Straight Corners">
                <VanillaResolver.instance.ToolButton
                    src={StraightIcon}
                    selected={straightCorners}
                    disabled={!straightCornersSupported}
                    tooltip={straightCornersSupported ? "Straighten  corners" : "Not supported for tracks"}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "SoftBlockToggleStraightCorners")}
                />
            </VanillaResolver.instance.Section>

            <VanillaResolver.instance.Section title="Traffic Direction">
                <VanillaResolver.instance.ToolButton
                    src={directionIcon}
                    selected={!isClockwise}
                    disabled={!isOneWaySupported}
                    tooltip={
                        !isOneWaySupported
                            ? "REQUIRES ONE-WAY ROAD"
                            : (!isClockwise ? "Counter-Clockwise Direction" : "Clockwise Direction")
                    }
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleMainDirection")}
                />
            </VanillaResolver.instance.Section>

            <VanillaResolver.instance.Section title="Remove Crosswalks">
                <VanillaResolver.instance.ToolButton
                    src={crossWalkIcon}
                    selected={suppressCrosswalks}
                    tooltip={suppressCrosswalks ? "Crosswalks are removed" : "Crosswalks are allowed"}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleSuppressCrosswalks")}
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