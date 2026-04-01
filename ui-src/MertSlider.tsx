import React, { useState, useRef, useEffect, useCallback } from 'react';
import styles from './MertSlider.module.scss';
import { trigger } from 'cs2/api';

interface MertSliderProps {
    value: number;
    min: number;
    max: number;
    step?: number;
    onChange: (val: number) => void;
    formatValue?: (val: number) => string;
}

export const MertSlider: React.FC<MertSliderProps> = ({
    value, min, max, step = 1, onChange, formatValue
}) => {
    const trackRef = useRef<HTMLDivElement>(null);
    const [isDragging, setIsDragging] = useState(false);



    // Matematiğin kalbi: Farenin konumunu değere çevirir
    const calculateValueFromMouse = useCallback((clientX: number) => {
        if (!trackRef.current) return value;
        
        const rect = trackRef.current.getBoundingClientRect();
        // X ekseninde kaçıncı pikseldeyiz (sınırları aşmasını engelle)
        const xPos = Math.max(0, Math.min(clientX - rect.left, rect.width));
        
        // Yüzde hesabı
        const percentage = xPos / rect.width;
        
        // Yüzdeyi min-max aralığındaki gerçek değere çevir
        const rawValue = min + percentage * (max - min);
        
        // Step (adım) değerine yuvarla
        const steppedValue = Math.round(rawValue / step) * step;
        
        // Son bir güvenlik (float hatalarına karşı)
        return Math.min(Math.max(steppedValue, min), max);
    }, [min, max, step, value]);

    const handleMouseDown = (e: React.MouseEvent) => {
        e.preventDefault();
        e.stopPropagation();
        trigger("MertsToolBox", "UiInteracted"); // Oyuna "ben buradayım odaklanma" mesajı
        
        setIsDragging(true);
        onChange(calculateValueFromMouse(e.clientX));
    };

    // Fare ekranın neresinde olursa olsun takibi bırakmamak için window eventleri
    useEffect(() => {
        if (!isDragging) return;

        const handleMouseMove = (e: MouseEvent) => {
            onChange(calculateValueFromMouse(e.clientX));
        };

        const handleMouseUp = () => {
            setIsDragging(false);
        };

        window.addEventListener('mousemove', handleMouseMove);
        window.addEventListener('mouseup', handleMouseUp);

        return () => {
            window.removeEventListener('mousemove', handleMouseMove);
            window.removeEventListener('mouseup', handleMouseUp);
        };
    }, [isDragging, calculateValueFromMouse, onChange]);

    const fillPercentage = Math.max(0, Math.min(100, ((value - min) / (max - min)) * 100));

    return (
        <div className={styles.sliderContainer} onContextMenu={(e) => e.stopPropagation()}>
            <div 
                className={`${styles.trackWrapper} ${isDragging ? styles.dragging : ''}`}
                ref={trackRef}
                onMouseDown={handleMouseDown}
            >
                <div className={styles.track}>
                    <div className={styles.fill} style={{ width: `${fillPercentage}%` }}></div>
                    <div className={styles.thumb} style={{ left: `${fillPercentage}%` }}></div>
                </div>
            </div>
            <div className={styles.valueDisplay}>
                {formatValue ? formatValue(value) : value.toString()}
            </div>
        </div>
    );
};