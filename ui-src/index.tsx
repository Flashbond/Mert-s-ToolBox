import { ModRegistrar } from "cs2/modding";
import React, { useEffect } from "react";
import { trigger, bindValue, useValue } from "cs2/api";
import { Tooltip } from "cs2/ui";
import { CirclePanelSection } from "./CirclePanelSection";
import { HelixPanelSection } from "./HelixPanelSection";
import { SuperEllipsePanelSection } from "./SuperEllipsePanelSection";
import { GridPanelSection } from "./GridPanelSection";
import { ToolBoxActionHints } from "./ToolBoxActionHints";
import { useVanillaClasses } from "./VanilliaResolver";
import styles from "./ToolBoxPanel.module.scss";

import circleIcon from "./Icons/Circle.svg";
import helixIcon from "./Icons/Helix.svg";
import superEllipseIcon from "./Icons/Ellipse.svg";
import gridIcon from "./Icons/Grid.svg";

type ToolId = "Circle" | "Helix" | "SuperEllipse" | "Grid";

type ToolDef = {
    id: ToolId;
    icon: string;
    tooltip: string;
};

const TOOL_DEFS: ToolDef[] = [
    { id: "Circle", icon: circleIcon, tooltip: "Perfect Circle" },
    { id: "Helix", icon: helixIcon, tooltip: "Helix Intersection" },
    { id: "SuperEllipse", icon: superEllipseIcon, tooltip: "Super Ellipse" },
    { id: "Grid", icon: gridIcon, tooltip: "Smart Grid" }
];

const PRELOAD_ICON_SRCS = TOOL_DEFS.map((tool) => tool.icon);

const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None");
const isToolBoxAllowed$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed", false);

let hasPreloadedIcons = false;

function preloadAllToolIcons() {
    if (hasPreloadedIcons) return;
    hasPreloadedIcons = true;

    PRELOAD_ICON_SRCS.forEach((src) => {
        const img = new Image();
        img.src = src;
    });
}

const ToolBoxModeRow = () => {
    const { itemClass, labelClass, contentClass, buttonClass, iconClass, iconButtonClass } = useVanillaClasses();
    const activeTool = useValue(activeToolMode$);

    return (
        <div className={itemClass}>
            <div className={labelClass}>Mert&apos;s ToolBox</div>

            <div className={contentClass}>
                {TOOL_DEFS.map((tool) => {
                    const isSelected = activeTool === tool.id;

                    return (
                        <Tooltip
                            key={tool.id}
                            tooltip={
                                <div>
                                    <div className="header_HpJ">
                                        <div className="title_lCJ">{tool.tooltip}</div>
                                    </div>
                                </div>
                            }
                        >
                            <button
                                className={`${buttonClass} ${iconButtonClass} ${isSelected ? "selected" : ""}`}
                                onMouseDown={(e) => {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    if (e.button !== 0) return;

                                    trigger("MertsToolBox", "ToggleTool", tool.id);
                                }}
                            >
                                <img
                                    className={iconClass}
                                    src={tool.icon}
                                    alt={tool.id}
                                    draggable={false}
                                />
                            </button>
                        </Tooltip>
                    );
                })}
            </div>
        </div>
    );
};

const register: ModRegistrar = (moduleRegistry) => {
    const mouseToolPath = "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx";

    moduleRegistry.extend(mouseToolPath, "MouseToolOptions", (OriginalMouseToolOptions: any) => {
        return (props: any) => {
            const vanillaClasses = useVanillaClasses();
            const isAllowed = useValue(isToolBoxAllowed$) as boolean;
            const activeTool = useValue(activeToolMode$) as string;
            const isActive = isAllowed && activeTool !== "None";

            useEffect(() => {
                preloadAllToolIcons();
            }, []);

            if (!isActive) {
                return (
                    <>
                        <OriginalMouseToolOptions {...props} />
                        {isAllowed && <ToolBoxModeRow />}
                    </>
                );
            }

            return (
                <div
                    className="merts-toolbox-root"
                    style={{
                        width: "100%",
                        display: "flex",
                        flexDirection: "column",
                        pointerEvents: "auto"
                    }}
                >
                    <ToolBoxActionHints />
                    <CirclePanelSection vanillaClasses={vanillaClasses} />
                    <HelixPanelSection vanillaClasses={vanillaClasses} />
                    <SuperEllipsePanelSection vanillaClasses={vanillaClasses} />
                    <GridPanelSection vanillaClasses={vanillaClasses} />
                </div>
            );
        };
    });
};

export default register;