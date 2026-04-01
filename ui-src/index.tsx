import { ModRegistrar } from "cs2/modding";
import React, { useEffect, useState } from "react";
import { trigger, bindValue, useValue } from "cs2/api";
import { Tooltip } from "cs2/ui";
import { CirclePanelSection } from "./CirclePanelSection";
import { HelixPanelSection } from "./HelixPanelSection";
import styles from "./ToolBoxPanel.module.scss";
import { ToolBoxActionHints } from "./ToolBoxActionHints";
import circleIcon from "./Icons/Circle.svg";
import helixIcon from "./Icons/Helix.svg";
import superEllipseIcon from "./Icons/Ellipse.svg";
import gridIcon from "./Icons/Grid.svg";
import { SuperEllipsePanelSection } from "./SuperEllipsePanelSection";
import { GridPanelSection } from "./GridPanelSection";

// --- TYPES & CONSTANTS ---

const uiPing = () => trigger("MertsToolBox", "UiInteracted");

type ToolId = "Circle" | "Helix" | "SuperEllipse" | "Grid";

type ToolDef = {
    id: ToolId;
    icon: string;
    tooltip: string;
};

type VanillaClasses = {
    buttonClass: string;
    iconClass: string;
    iconButtonClass: string;
    startButtonClass: string;
    endButtonClass: string;
    numberFieldClass: string;
    indicatorClass: string;
    isReady: boolean;
};

const TOOL_DEFS: ToolDef[] = [
    { id: "Circle", icon: circleIcon, tooltip: "Perfect Circle" },
    { id: "Helix", icon: helixIcon, tooltip: "Helix Intersection" },
    { id: "SuperEllipse", icon: superEllipseIcon, tooltip: "Super Ellipse" },
    { id: "Grid", icon: gridIcon, tooltip: "Smart Grid" }
];

const PRELOAD_ICON_SRCS = TOOL_DEFS.map((tool) => tool.icon);

// --- GLOBAL STATE BINDINGS ---

const activeToolMode$ = bindValue<string>("MertsToolBox", "ActiveTool", "None");
const isToolBoxAllowed$ = bindValue<boolean>("MertsToolBox", "IsToolBoxAllowed", false);

// --- UTILITIES: ICON PRELOADER ---

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
            className="mpc-icon-preloader"
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

// --- UTILITIES: VANILLA CLASS SCRAPER ---
// Dynamically extracts generated CSS classes from the vanilla UI to ensure seamless visual integration.

const vanillaClassesCache: VanillaClasses = {
    buttonClass: "button_yDV button_yDV",
    iconClass: "icon_okL",
    iconButtonClass: "icon-button_fSD",
    startButtonClass: "",
    endButtonClass: "",
    numberFieldClass: "",
    indicatorClass: "",
    isReady: false
};

const classListeners = new Set<() => void>();
let isPolling = false;

const startPollingVanillaClasses = () => {
    if (isPolling) return;
    isPolling = true;

    const extractClass = (element: Element | null, prefix: string): string => {
        if (!element) return "";
        for (let i = 0; i < element.classList.length; i++) {
            if (element.classList[i].startsWith(prefix)) return element.classList[i];
        }
        return "";
    };

    const stealClasses = () => {
        const panel = document.querySelector('[class*="tool-options-panel_"]') || document;

        const newButtonClass = extractClass(panel.querySelector('button[class*="button_"]'), 'button_');
        const newIconClass = extractClass(panel.querySelector('button img[class*="icon_"]'), 'icon_');
        const newIconButtonClass = extractClass(panel.querySelector('button[class*="icon-button_"]'), 'icon-button_');
        const newStartButtonClass = extractClass(panel.querySelector('button[class*="start-button_"]'), 'start-button_');
        const newEndButtonClass = extractClass(panel.querySelector('button[class*="end-button_"]'), 'end-button_');
        const newNumberFieldClass = extractClass(panel.querySelector('div[class*="number-field"]'), 'number-field');
        const newIndicatorClass = extractClass(panel.querySelector('svg[class*="indicator_"]'), 'indicator_');

        if (newButtonClass && newNumberFieldClass) {
            let hasThemeChanged = false;

            if (vanillaClassesCache.buttonClass !== newButtonClass) { vanillaClassesCache.buttonClass = newButtonClass; hasThemeChanged = true; }
            if (vanillaClassesCache.iconClass !== newIconClass) { vanillaClassesCache.iconClass = newIconClass; hasThemeChanged = true; }
            if (vanillaClassesCache.iconButtonClass !== newIconButtonClass) { vanillaClassesCache.iconButtonClass = newIconButtonClass; hasThemeChanged = true; }
            if (vanillaClassesCache.numberFieldClass !== newNumberFieldClass) { vanillaClassesCache.numberFieldClass = newNumberFieldClass; hasThemeChanged = true; }
            if (vanillaClassesCache.startButtonClass !== newStartButtonClass) { vanillaClassesCache.startButtonClass = newStartButtonClass; hasThemeChanged = true; }
            if (vanillaClassesCache.endButtonClass !== newEndButtonClass) { vanillaClassesCache.endButtonClass = newEndButtonClass; hasThemeChanged = true; }
            if (vanillaClassesCache.indicatorClass !== newIndicatorClass) { vanillaClassesCache.indicatorClass = newIndicatorClass; hasThemeChanged = true; }

            if (!vanillaClassesCache.isReady || hasThemeChanged) {
                vanillaClassesCache.isReady = true;
                classListeners.forEach(l => l());
            }
        }
    };

    // Aggressive polling on mount to quickly grab classes before the user interacts
    let attempts = 0;
    const intervalId = setInterval(() => {
        stealClasses();
        attempts++;
        if (attempts > 20) clearInterval(intervalId);
    }, 100);

    // Debounced observer to catch late renders or theme swaps
    let debounceTimer: ReturnType<typeof setTimeout> | null = null;
    const observer = new MutationObserver(() => {
        if (debounceTimer) clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => stealClasses(), 150);
    });

    observer.observe(document.body, { childList: true, subtree: true });
};

export const useVanillaClasses = (): VanillaClasses => {
    const [classes, setClasses] = useState<VanillaClasses>({ ...vanillaClassesCache });

    useEffect(() => {
        const update = () => setClasses({ ...vanillaClassesCache });
        classListeners.add(update);
        startPollingVanillaClasses();

        return () => {
            classListeners.delete(update);
        };
    }, []);

    return classes;
};

// --- UI COMPONENTS ---

const ToolBoxModeRow = () => {
    const { buttonClass, iconClass, iconButtonClass } = useVanillaClasses();
    const activeTool = useValue(activeToolMode$);

    return (
        <div className={`item_bZY`}>
            <div className={styles.rowLabel}>Mert&apos;s ToolBox</div>

            <div className={`content_ZIz`}>
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

// --- MOD REGISTRAR (ENTRY POINT) ---
// Injects custom React components into the game's UI hierarchy via module extensions.

const register: ModRegistrar = (moduleRegistry) => {
    const mouseToolPath = "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx";

    // 1. Root Wrapper: Injects panels and handles global UI interaction pings
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

    // 2. Section Injection: Mounts the main tool row and manages vanilla section visibility
    moduleRegistry.extend(mouseToolPath, "Section", (OriginalSection: any) => {
        return (props: any) => {
            const isAllowed = useValue(isToolBoxAllowed$);
            const activeTool = useValue(activeToolMode$);
            const isActive = isAllowed && activeTool !== "None";

            // Safely clone children to avoid crash with Anarchy or other mods
            const mutableChildren = Array.isArray(props.children)
                ? [...props.children]
                : (props.children !== undefined && props.children !== null ? [props.children] : []);

            const safeProps = { ...props, children: mutableChildren };
            function isTopographyByChildren(children: any): boolean {
                try {
                    const str = JSON.stringify(children ?? "");
                    return str.includes("ContourLines");
                } catch {
                    return false;
                }
            }

            const isTopographySection = isTopographyByChildren(safeProps.children);

           
            if (!isAllowed) return <OriginalSection {...safeProps} />;

            // Purge vanilla tool options from the DOM when a custom tool is active
            if (isActive) {
                return <></>;
            }

            // Append custom tool selection row immediately after the topography section
            if (isTopographySection) {
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