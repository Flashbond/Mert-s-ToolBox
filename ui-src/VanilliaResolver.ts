import { useEffect, useState } from "react";

export type VanillaClasses = {
    itemClass: string;
    labelClass: string;
    contentClass: string;
    buttonClass: string;
    iconClass: string;
    iconButtonClass: string;
    startButtonClass: string;
    endButtonClass: string;
    numberFieldClass: string;
    indicatorClass: string;
};

const fallbackClasses: VanillaClasses = {
    itemClass: "item_bZY",
    labelClass: "label_RZX",
    contentClass: "content_ZIz",
    buttonClass: "button_yDV button_yDV",
    iconClass: "icon_okL",
    iconButtonClass: "icon-button_fSD",
    startButtonClass: "",
    endButtonClass: "",
    numberFieldClass: "",
    indicatorClass: ""
};

function extractClass(element: Element | null, prefix: string): string {
    if (!element) return "";

    for (let i = 0; i < element.classList.length; i++) {
        const cls = element.classList[i];
        if (cls.startsWith(prefix)) return cls;
    }

    return "";
}

function readVanillaClasses(): VanillaClasses {
    const panel = document.querySelector('[class*="tool-options-panel_"]') || document;

    const itemClass = extractClass(panel.querySelector('div[class*="item"]'), "item_");
    const labelClass = extractClass(panel.querySelector('div[class*="label"]'), "label_");
    const contentClass = extractClass(panel.querySelector('div[class*="content"]'), "content_");
    const buttonClass = extractClass(panel.querySelector('button[class*="button_"]'), "button_");
    const iconClass = extractClass(panel.querySelector('button img[class*="icon_"]'), "icon_");
    const iconButtonClass = extractClass(panel.querySelector('button[class*="icon-button_"]'), "icon-button_");
    const startButtonClass = extractClass(panel.querySelector('button[class*="start-button_"]'), "start-button_");
    const endButtonClass = extractClass(panel.querySelector('button[class*="end-button_"]'), "end-button_");
    const numberFieldClass = extractClass(panel.querySelector('div[class*="number-field"]'), "number-field");
    const indicatorClass = extractClass(panel.querySelector('svg[class*="indicator_"]'), "indicator_");

    return {
        itemClass: itemClass || fallbackClasses.itemClass,
        labelClass: labelClass || fallbackClasses.labelClass,
        contentClass: contentClass || fallbackClasses.contentClass,
        buttonClass: buttonClass ? `${buttonClass} ${buttonClass}` : fallbackClasses.buttonClass,
        iconClass: iconClass || fallbackClasses.iconClass,
        iconButtonClass: iconButtonClass || fallbackClasses.iconButtonClass,
        startButtonClass,
        endButtonClass,
        numberFieldClass,
        indicatorClass
    };
}

export function useVanillaClasses(): VanillaClasses {
    const [classes, setClasses] = useState<VanillaClasses>(readVanillaClasses());

    useEffect(() => {
        const refresh = () => setClasses(readVanillaClasses());

        refresh();

        let timeoutId: ReturnType<typeof setTimeout> | null = null;
        const observer = new MutationObserver(() => {
            if (timeoutId) clearTimeout(timeoutId);
            timeoutId = setTimeout(refresh, 100);
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ["class", "style"]
        });

        return () => {
            observer.disconnect();
            if (timeoutId) clearTimeout(timeoutId);
        };
    }, []);

    return classes;
}