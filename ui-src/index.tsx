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

const uiPing = () => trigger("MertsToolBox", "UiInteracted");

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

const IconPreloader = () => {
    useEffect(() => {
        preloadAllToolIcons();
    }, []);

    return (
        <div
            style={{
                position: "absolute",
                width: "0",
                height: "0",
                overflow: "hidden",
                opacity: 0,
                pointerEvents: "none"
            }}
        >
            {PRELOAD_ICON_SRCS.map((src, idx) => (
                <img key={idx} src={src} alt="" width={1} height={1} aria-hidden="true" draggable={false} />
            ))}
        </div>
    );
};

function getDisplayNameFromTitle(title: any): string {
    try {
        const direct = title?.type?.displayName;
        if (typeof direct === "string") return direct;

        const memoType = title?.type?.type?.displayName;
        if (typeof memoType === "string") return memoType;

        const renderString = title?.type?.renderString;
        if (typeof renderString === "function") {
            const rendered = renderString();
            if (typeof rendered === "string") return rendered;
        }
    } catch {
        // ignore
    }

    return "";
}

function isContourLinesSection(title: any): boolean {
    return getDisplayNameFromTitle(title).includes("CONTOUR_LINES");
}

function panelHasGridSvg(): boolean {
    try {
        const panel = document.querySelector('[class*="tool-options-panel_"]');
        if (!panel) return false;

        return !!panel.querySelector('img[src*="Grid.svg"]');
    } catch {
        return false;
    }
}

const ToolBoxModeRow = () => {
    const { buttonClass, iconClass, iconButtonClass } = useVanillaClasses();
    const activeTool = useValue(activeToolMode$);

    return (
        <div className="item_bZY">
            <div className={styles.rowLabel}>Mert&apos;s ToolBox</div>

            <div className="content_ZIz">
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

            return (
                <div
                    className="merts-toolbox-root"
                    onMouseDownCapture={() => uiPing()}
                    onMouseUpCapture={() => uiPing()}
                    onClickCapture={() => uiPing()}
                    onContextMenuCapture={() => uiPing()}
                    style={{ width: "100%", display: "flex", flexDirection: "column", pointerEvents: "auto" }}
                >
                    <IconPreloader />
                    <OriginalMouseToolOptions {...props} />
                    <ToolBoxActionHints />
                    <CirclePanelSection vanillaClasses={vanillaClasses} />
                    <HelixPanelSection vanillaClasses={vanillaClasses} />
                    <SuperEllipsePanelSection vanillaClasses={vanillaClasses} />
                    <GridPanelSection vanillaClasses={vanillaClasses} />
                </div>
            );
        };
    });

    moduleRegistry.extend(mouseToolPath, "Section", (OriginalSection: any) => {
        return (props: any) => {
            const isAllowed = useValue(isToolBoxAllowed$) as boolean;
            const activeTool = useValue(activeToolMode$) as string;
            const isActive = isAllowed && activeTool !== "None";

            const mutableChildren = Array.isArray(props.children)
                ? [...props.children]
                : (props.children !== undefined && props.children !== null ? [props.children] : []);

            const safeProps = { ...props, children: mutableChildren };

            const isContourSection = isContourLinesSection(props.title);
            const hasGridSvg = panelHasGridSvg();

            const shouldInjectToolBoxRow =
                isAllowed &&
                !isActive &&
                isContourSection &&
                hasGridSvg;

            if (!isAllowed) return <OriginalSection {...safeProps} />;

            if (isActive) {
                return <></>;
            }

            if (shouldInjectToolBoxRow) {
                return (
                    <>
                        <OriginalSection {...safeProps} />
                        <ToolBoxModeRow />
                    </>
                );
            }

            return <OriginalSection {...safeProps} />;
        };
    });
};

export default register;