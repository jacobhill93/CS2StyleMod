import { Button } from "cs2/ui";
import styles from "./SearchBox.module.scss";

// Gameface doesn't render the native `placeholder` attribute (the old raw
// <input placeholder="Search name"> showed as an empty box in-game), so the
// placeholder is an overlaid <span> instead - same approach as Road
// Builder's SearchTextBox.
export const SearchBox = ({
    value,
    onChange,
    placeholder = "Search...",
}: {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
}) => (
    <div className={styles.searchBox}>
        <input className={styles.input} value={value} onChange={(event) => onChange(event.target.value)} />
        {value === "" && <span className={styles.placeholder}>{placeholder}</span>}
        {value !== "" && (
            <Button variant="icon" className={styles.clear} onSelect={() => onChange("")}>
                <img style={{ maskImage: "url(Media/Glyphs/Close.svg)" }} />
            </Button>
        )}
    </div>
);
