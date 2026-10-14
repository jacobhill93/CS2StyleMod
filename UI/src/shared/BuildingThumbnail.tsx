import styles from "shared/BuildingThumbnail.module.scss";

interface BuildingThumbnailProps {
    iconPath: string;
    // Optional size override (e.g. "var(--assetMenuImageSize)" for
    // UI_UPGRADE_GUIDE.md Phase 4c's tile grid, instead of this
    // component's normal fixed 48px) - applied as an inline style, not a
    // second className, since BuildingThumbnail.module.scss's own
    // width/height rule has identical specificity (one class each) to
    // any override class a caller could pass, making cascade order
    // between two different SCSS modules unpredictable; an inline style
    // always wins unambiguously instead. The placeholder-vs-broken-image
    // logic below stays identical either way, not duplicated per caller.
    size?: string;
}

// Shared by the Assets tab and the Collection detail's membership list -
// see BuildingThumbnail.module.scss for why a placeholder (not nothing,
// not a broken image) is the right default.
export const BuildingThumbnail = ({ iconPath, size }: BuildingThumbnailProps) => {
    const sizeStyle = size ? { width: size, height: size } : undefined;
    return iconPath ? (
        <img src={iconPath} className={styles.thumbnail} style={sizeStyle} />
    ) : (
        <div className={styles.placeholder} style={sizeStyle} />
    );
};
