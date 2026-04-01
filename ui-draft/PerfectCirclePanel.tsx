import * as React from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import styles from "./PerfectCirclePanel.module.scss";

const radius$ = bindValue<number>("MertsPerfectCircle", "Radius");
const available$ = bindValue<boolean>("MertsPerfectCircle", "Available");
const previewActive$ = bindValue<boolean>("MertsPerfectCircle", "PreviewActive");
const status$ = bindValue<string>("MertsPerfectCircle", "Status");

function clampRadius(value: number): number {
    if (Number.isNaN(value)) return 10;
    return Math.max(10, Math.min(300, value));
}

export const PerfectCirclePanel: React.FC = () => {
    const radius = useValue(radius$) ?? 80;
    const available = useValue(available$) ?? false;
    const previewActive = useValue(previewActive$) ?? false;
    const status = useValue(status$) ?? "Idle";

    const [draftRadius, setDraftRadius] = React.useState<string>(String(radius));

    React.useEffect(() => {
        setDraftRadius(String(radius));
    }, [radius]);

    const onCreate = React.useCallback(() => {
        trigger("MertsPerfectCircle", "CreateCircle");
    }, []);

    const onRadiusMinus = React.useCallback(() => {
        trigger("MertsPerfectCircle", "RadiusMinus");
    }, []);

    const onRadiusPlus = React.useCallback(() => {
        trigger("MertsPerfectCircle", "RadiusPlus");
    }, []);

    const applyDraftRadius = React.useCallback(() => {
        const parsed = parseInt(draftRadius, 10);
        trigger("MertsPerfectCircle", "SetRadius", clampRadius(parsed));
    }, [draftRadius]);

    const onInputKeyDown = React.useCallback(
        (e: React.KeyboardEvent<HTMLInputElement>) => {
            if (e.key === "Enter") {
                applyDraftRadius();
                (e.target as HTMLInputElement).blur();
            }
        },
        [applyDraftRadius]
    );

    if (!available) {
        return null;
    }

    return (
        <div className={styles.panel}>
            <div className={styles.header}>
                <div className={styles.title}>Perfect Circle</div>
                <div className={previewActive ? styles.badgeReady : styles.badgeIdle}>
                    {previewActive ? "Preview Ready" : "Ready"}
                </div>
            </div>

            <div className={styles.row}>
                <div className={styles.label}>Radius</div>

                <div className={styles.controls}>
                    <button className={styles.stepButton} onClick={onRadiusMinus}>
                        -
                    </button>

                    <input
                        className={styles.input}
                        type="number"
                        min={10}
                        max={300}
                        step={5}
                        value={draftRadius}
                        onChange={(e) => setDraftRadius(e.target.value)}
                        onBlur={applyDraftRadius}
                        onKeyDown={onInputKeyDown}
                    />

                    <button className={styles.stepButton} onClick={onRadiusPlus}>
                        +
                    </button>
                </div>
            </div>

            <div className={styles.row}>
                <button className={styles.createButton} onClick={onCreate}>
                    Create Circle
                </button>
            </div>

            <div className={styles.footer}>
                <span className={styles.statusLabel}>Status:</span>
                <span className={styles.statusText}>{status}</span>
            </div>
        </div>
    );
};