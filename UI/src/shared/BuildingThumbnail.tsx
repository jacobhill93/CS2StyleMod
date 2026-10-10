import styles from "shared/BuildingThumbnail.module.scss";

interface BuildingThumbnailProps {
    iconPath: string;
}

// Shared by the Assets tab and the Collection detail's membership list -
// see BuildingThumbnail.module.scss for why a placeholder (not nothing,
// not a broken image) is the right default.
export const BuildingThumbnail = ({ iconPath }: BuildingThumbnailProps) =>
    iconPath ? <img src={iconPath} className={styles.thumbnail} /> : <div className={styles.placeholder} />;
