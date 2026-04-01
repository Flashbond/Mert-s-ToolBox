import React, { useEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";
import { useValue, bindValue } from "cs2/api";


const moduleName = "MertsToolBox";

const activeTool$ = bindValue<string>(moduleName, "ActiveTool", "None");
const showCircleCtrlWheelHint$ = bindValue<boolean>(moduleName, "ShowCircleCtrlWheelHint", false);
const showHelixCtrlWheelHint$ = bindValue<boolean>(moduleName, "ShowHelixCtrlWheelHint", false);
const showSuperEllipseCtrlWheelHint$ = bindValue<boolean>(moduleName, "ShowSuperEllipseCtrlWheelHint", false);
const actionStatusText$ = bindValue<string>(moduleName, "ActionStatusText", "");
const MPC_HINT_HOST_ATTR = "data-mpc-action-hint-root";

function isTooltipVisible(el: HTMLElement | null): boolean {
    if (!el) return false;
    const style = window.getComputedStyle(el);
    if (style.display === "none") return false;
    if (style.visibility === "hidden") return false;
    if (style.opacity === "0") return false;
    return true;
}

function resolveTooltipGroup(): HTMLElement | null {
    const tooltip = document.getElementById("tooltip-0");
    if (!tooltip || !isTooltipVisible(tooltip)) return null;
    const group = tooltip.querySelector(".group_zcS") as HTMLElement | null;
    return group ?? tooltip;
}

function ensurePortalHost(parent: HTMLElement): HTMLElement {
    let host = parent.querySelector(`[${MPC_HINT_HOST_ATTR}="true"]`) as HTMLElement | null;
    if (!host) {
        host = document.createElement("div");
        host.setAttribute(MPC_HINT_HOST_ATTR, "true");
        parent.appendChild(host);
    }
    if (parent.lastElementChild !== host) {
        parent.appendChild(host);
    }
    return host;
}

export const ToolBoxActionHints = () => {
    const [portalHost, setPortalHost] = useState<HTMLElement | null>(null);
    const activeTool = useValue(activeTool$);
    const showCircleCtrlWheelHint = useValue(showCircleCtrlWheelHint$) as boolean;
    const showHelixCtrlWheelHint = useValue(showHelixCtrlWheelHint$) as boolean;
    const showSuperEllipseCtrlWheelHint = useValue(showSuperEllipseCtrlWheelHint$) as boolean;
    const actionStatusText = useValue(actionStatusText$) as string;
    useEffect(() => {
        const syncHost = () => {
            const parent = resolveTooltipGroup();
            if (!parent) {
                setPortalHost(null);
                return;
            }
            const host = ensurePortalHost(parent);
            setPortalHost(host);
        };

        syncHost();
        const observer = new MutationObserver(() => syncHost());
        observer.observe(document.body, {
            childList: true, subtree: true, attributes: true, attributeFilter: ["class", "style", "id"]
        });
        return () => observer.disconnect();
    }, []);

    const content = useMemo(() => {
        
        if (activeTool === "None") return null;

        let actionText = "";
        let statusText = actionStatusText;
        let showCtrlWheelHint = false;

        switch (activeTool) {
            case "Circle":
                actionText = "Precise Diameter";
                showCtrlWheelHint = showCircleCtrlWheelHint;
                break;

            case "Helix":
                actionText = "Precise Turns";
                showCtrlWheelHint = showHelixCtrlWheelHint;
                break;

            case "SuperEllipse":
                actionText = "Precise Shape";
                showCtrlWheelHint = showSuperEllipseCtrlWheelHint;
                break;

            case "Grid":
                actionText = "";
                showCtrlWheelHint = false;
                break;
        }

        return (
            <div className="row-item_oHU item_k3Z tooltip-base_zwi">
                <span style={{ alignItems: "flex-start", flexDirection: "column" }}>

                    {showCtrlWheelHint && (
                        <span className="hint_l_F">
                            <span className="modifier_iDc">
                                <span className="button-text_fw1 button_ehL" style={{ padding: "0 0.6em 0 0.7em" }}>Ctrl</span>
                            </span>
                            <span className="binding_dc_">
                                <img className="hint-icon_VtT" src="Media/Mouse/Scrollwheel.svg" alt="Wheel" />
                            </span>
                            <span className="hint-label_c1x">
                                <div className="paragraphs_nbD paragraphs_LK4">
                                    <p className="p_CKq" cohinline="cohinline">{actionText}</p>
                                </div>
                            </span>
                        </span>
                    )}

                    <span className="hint-label_c1x">
                        <div className="p_CKq" cohinline="cohinline">
                            <div style={{marginTop: "4px" }}>{statusText}</div>
                        </div>
                    </span>
                </span>
            </div>
        );
    }, [
        activeTool,
        showCircleCtrlWheelHint,
        showHelixCtrlWheelHint,
        showSuperEllipseCtrlWheelHint,
        actionStatusText
    ]);

    if (!portalHost || activeTool === "None") return null;

    return createPortal(content, portalHost);
};