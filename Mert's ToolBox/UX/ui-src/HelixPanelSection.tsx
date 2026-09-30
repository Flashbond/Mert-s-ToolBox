import React, { useEffect, useState } from "react";
import { trigger, call, bindValue, useValue } from "cs2/api";
import mertsToolBoxIcon from "./icons/MertsToolBox.svg";
import standardAnarchyIcon from "./icons/StandardAnarchy.svg";
import coloredAnarchyIcon from "./icons/ColoredAnarchy.svg";
import directionIcon from "./icons/Direction.svg";
import ccwIcon from "./icons/CounterCW.svg";
import crossWalkIcon from "./icons/CrossWalk.svg";
import { formatMeters, formatSmart } from "./utils/Formatters";
import { VanillaResolver } from "./utils/VanilliaResolver";
import { parseActiveTool, ActiveTool } from "./utils/ActiveTool";
import { MertListBox } from './utils/MertListBox';

// --- GLOBAL BINDINGS (C# TO UI) ---
const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None|None");
const toolBoxVisible$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed");

const helixAnarchyInstalled$ = bindValue<boolean>("MertsToolBox", "HelixAnarchyInstalled", false);
const helixBuildMode$ = bindValue<number>("MertsToolBox", "HelixBuildMode", 0);

const helixDiameter$ = bindValue<number>("MertsToolBox", "HelixDiameter");
const helixDiameterStepValue$ = bindValue<number>("MertsToolBox", "HelixDiameterStepValue");
const helixDiameterStepArray$ = bindValue<number[]>("MertsToolBox", "HelixDiameterStepArray");

const helixTurn$ = bindValue<number>("MertsToolBox", "HelixTurns");
const helixTurnStepValue$ = bindValue<number>("MertsToolBox", "HelixTurnStepValue");
const helixTurnStepArray$ = bindValue<number[]>("MertsToolBox", "HelixTurnStepArray");

const helixClearance$ = bindValue<number>("MertsToolBox", "HelixClearance");
const helixClearanceStepValue$ = bindValue<number>("MertsToolBox", "HelixClearanceStepValue");
const helixClearanceStepArray$ = bindValue<number[]>("MertsToolBox", "HelixClearanceStepArray");

const helixIsAscending$ = bindValue<boolean>("MertsToolBox", "MainDirectionState", false);
const helixIsOneWaySupported$ = bindValue<boolean>("MertsToolBox", "HelixIsOneWaySupported", false);
const helixIsClockwise$ = bindValue<boolean>("MertsToolBox", "HelixIsClockwise", false);

const suppressCrosswalks$ = bindValue<boolean>("MertsToolBox", "SuppressCrosswalks", false);

const elevationValue$ = bindValue<number>("MertsToolBox", "ElevationValue");
const elevationStepValue$ = bindValue<number>("MertsToolBox", "ElevationStepValue");
const elevationStepArray$ = bindValue<number[]>("MertsToolBox", "ElevationStepArray");

const presetList$ = bindValue<string>("MertsToolBox", "PresetList", "");

// --- COMPONENT DEFINITION ---
export const HelixPanelSection = () => {

    // --- VISIBILITY & LIFECYCLE ---
    const activeToolRaw = useValue(activeToolMode$) as string;
    const activeTool = parseActiveTool(activeToolRaw);

    const isToolBoxAllowed = useValue(toolBoxVisible$) as boolean;
    const rawShow: boolean = isToolBoxAllowed && activeTool.id === "Helix";
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

    const anarchyInstalled = useValue(helixAnarchyInstalled$) as boolean;
    const buildMode = useValue(helixBuildMode$) as number;
    const anarchySelected = anarchyInstalled && buildMode === 1;
    const toolboxSelected = !anarchySelected;

    // --- DATA BINDING EVALUATION ---
    const diameter = useValue(helixDiameter$) as number;
    const diameterStepValue = useValue(helixDiameterStepValue$) as number;
    const diameterStepValues = useValue(helixDiameterStepArray$) as number[];

    const turn = useValue(helixTurn$) as number;
    const turnStepValue = useValue(helixTurnStepValue$) as number;
    const turnStepValues = useValue(helixTurnStepArray$) as number[];

    const clearance = useValue(helixClearance$) as number;
    const clearanceStepValue = useValue(helixClearanceStepValue$) as number;
    const clearanceStepValues = useValue(helixClearanceStepArray$) as number[];
    const isOneWaySupported = useValue(helixIsOneWaySupported$) as boolean;
    const isAscending = useValue(helixIsAscending$) as boolean;
    const isClockwise = useValue(helixIsClockwise$) as boolean;

    const suppressCrosswalks = useValue(suppressCrosswalks$) as boolean;

    const elevationValue = useValue(elevationValue$) as number;
    const elevationStepValue = useValue(elevationStepValue$) as number;
    const elevationStepValues = useValue(elevationStepArray$) as number[];

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

            {/* BUILDING MODE OPTIONS ROW */}
            <VanillaResolver.instance.Section title="Building Mode">
                <VanillaResolver.instance.ToolButton
                    src={anarchySelected ? coloredAnarchyIcon : standardAnarchyIcon}
                    selected={anarchySelected}
                    disabled={!anarchyInstalled}
                    tooltip={
                        !anarchyInstalled
                            ? "Requires Anarchy installation."
                            : (anarchySelected ? "Anarchy is enabled." : "Anarchy is disabled.")
                    }
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixSetBuildMode", 1)}
                />
                <VanillaResolver.instance.ToolButton
                    src={mertsToolBoxIcon}
                    selected={toolboxSelected}
                    tooltip={
                        toolboxSelected
                            ? "Using Mert's ToolBox (EXPERIMENTAL. Anarchy is recommended.)"
                            : "Mert's ToolBox is disabled (Enabling is NOT RECOMMENDED.)"
                    }
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixSetBuildMode", 0)}
                />
            </VanillaResolver.instance.Section>

            {/* DIAMETER ROW */}
            <VanillaResolver.instance.Section title="Diameter">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixDiameterDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>{formatMeters(diameter)}</div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixDiameterUp")}
                />

                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={diameterStepValue}
                    values={diameterStepValues}
                    tooltip={`${diameterStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "HelixDiameterStep", val);
                    }}
                />
            </VanillaResolver.instance.Section>

            {/* TURNS ROW */}
            <VanillaResolver.instance.Section title="Turns">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixTurnsDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>{formatSmart(turn)}</div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixTurnsUp")}
                />
                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={turnStepValue}
                    values={turnStepValues}
                    tooltip={`${turnStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "HelixTurnStep", val);
                    }}
                />
            </VanillaResolver.instance.Section>

            {/* CLEARANCE ROW */}
            <VanillaResolver.instance.Section title="Clearance">
                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixClearanceDown")}
                />

                <div className={VanillaResolver.instance.mouseToolOptionsTheme["number-field"]}>{formatMeters(clearance)}</div>

                <VanillaResolver.instance.ToolButton
                    src="Media/Glyphs/ThickStrokeArrowUp.svg"
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixClearanceUp")}
                />
                <VanillaResolver.instance.StepToolButton
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    selectedValue={clearanceStepValue}
                    values={clearanceStepValues}
                    tooltip={`${clearanceStepValue}`}
                    onSelect={(val) => {
                        trigger("MertsToolBox", "HelixClearanceStep", val);
                    }}
                />
            </VanillaResolver.instance.Section>

            <VanillaResolver.instance.Section title="Climbing Direction">
                <VanillaResolver.instance.ToolButton
                    src={directionIcon}
                    selected={!isAscending}
                    disabled={!isOneWaySupported}
                    tooltip={
                        !isOneWaySupported
                            ? "REQUIRES ONE-WAY ROAD"
                            : (!isAscending ? "Downhill" : "Uphill")
                    }
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "ToggleMainDirection")}
                />
            </VanillaResolver.instance.Section>

            <VanillaResolver.instance.Section title="Helix Direction">
                <VanillaResolver.instance.ToolButton
                    src={ccwIcon}
                    selected={!isClockwise}
                    tooltip={!isClockwise ? "Turns counter-clockwise" : "Turns clockwise"}
                    focusKey={VanillaResolver.instance.FOCUS_DISABLED}
                    onSelect={() => trigger("MertsToolBox", "HelixToggleDirection")}
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