import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel } from "cs2/ui";
import { useState } from "react";
import classNames from "classnames";
import mod from "../../../mod.json";
import { fpsMeterLevel$ } from "shared/fpsMeter";
import { closeShell, shellVisible$ } from "shared/shellVisibility";
import { TextButton } from "shared/TextButton";
import { ZonesTab } from "./tabs/ZonesTab";
import { CollectionsTab } from "./tabs/CollectionsTab";
import { AssetsTab } from "./tabs/AssetsTab";
import styles from "./ShellFeature.module.scss";

// Phase 5 (UI_UPGRADE_GUIDE.md) - minimal failure surfacing. Mirrors
// CollectionUIEntry/CollectionLevel1WarningEntry (src/UI/UIErrorState.cs)
// exactly - a failed CollectionUISystem/ZoneUISystem save or delete
// (disk full, file locked, permissions denied - previously logged only,
// invisible to the player) now reaches this one binding instead.
interface UIErrorEntry {
    active: boolean;
    message: string;
}

const UIError$ = bindValue<UIErrorEntry>(mod.id, "UIError", { active: false, message: "" });

type ShellTab = "zones" | "collections" | "assets";

const TABS: { id: ShellTab; label: string }[] = [
    { id: "zones", label: "Zones" },
    { id: "collections", label: "Collections" },
    { id: "assets", label: "Assets" },
];

// Phase 1 of UI_OVERHAUL_PLAN.md: one fixed-position (not draggable - see
// decision #1), single Panel replacing the four independent floating
// panels, with a client-side tab bar. Registered alone in index.tsx in
// place of CollectionsFeature/ZonesFeature - those panels' files stay in
// the repo, unregistered, until their replacements are confirmed working
// in-game (see "What NOT to do" in the plan).
export const ShellFeature = () => {
    const fpsMeterLevel = useValue(fpsMeterLevel$);
    const uiError = useValue(UIError$);
    const [activeTab, setActiveTab] = useState<ShellTab>("zones");
    // Which collection the Assets tab is currently picking for, if any -
    // owned here (not by CollectionsTab/AssetsTab) since switching into
    // and out of picking mode also switches the active tab. There was
    // briefly a separate AssetPickerDialog floating panel instead of
    // this; merged away after its first in-game test surfaced a real
    // positioning bug (nested inside the shell's own position:absolute
    // wrapper, it rendered against the SHELL's box, not the screen) and
    // it became clear the two views only differed by a few conditional
    // props - not enough to justify a second floating panel.
    const [focusedCollectionId, setFocusedCollectionId] = useState<string | null>(null);
    const visible = useValue(shellVisible$);

    const switchTab = (tab: ShellTab) => {
        setActiveTab(tab);
        setFocusedCollectionId(null);
    };

    const startAddingAssets = (collectionId: string) => {
        setFocusedCollectionId(collectionId);
        setActiveTab("assets");
    };

    const finishAddingAssets = () => {
        // Fires the one-shot Level 1 check here, not per add/remove click -
        // see DECISIONS.md "Level 1 warning moved to the collection, not
        // the zone attach": the user is "finalizing asset addition" the
        // moment they click Done, which is exactly this call site.
        if (focusedCollectionId) trigger(mod.id, "FinishEditingCollectionAssets", focusedCollectionId);

        // Keep focusedCollectionId set - CollectionsTab reads it back as
        // initialSelectedCollectionId so the same collection's detail
        // view reopens instead of dropping back to the list. Cleared the
        // next time any tab button is clicked (switchTab above).
        setActiveTab("collections");
    };

    // After every hook, not before - still mounted (via UniversalModMenuToggle)
    // even while hidden, so activeTab/focusedCollectionId survive a close/reopen.
    if (!visible) return null;

    return (
        <div className={classNames(styles.shell, styles[`fpsLevel${fpsMeterLevel}`])}>
            <Panel header={<div className={styles.header}>ZoneBuilder</div>} onClose={closeShell}>
                {uiError.active && (
                    <div className={styles.errorBanner}>
                        <span className={styles.errorBannerText}>{uiError.message}</span>
                        <div className={styles.errorBannerActions}>
                            <TextButton onSelect={() => trigger(mod.id, "DismissUIError")}>OK</TextButton>
                        </div>
                    </div>
                )}
                <div className={styles.tabs}>
                    {TABS.map((tab) => (
                        <Button
                            key={tab.id}
                            variant="flat"
                            className={classNames(styles.tabButton, activeTab === tab.id && styles.tabButtonSelected)}
                            selected={activeTab === tab.id}
                            onSelect={() => switchTab(tab.id)}
                        >
                            {tab.label}
                        </Button>
                    ))}
                </div>
                <div className={styles.body}>
                    {activeTab === "zones" && <ZonesTab />}
                    {activeTab === "collections" && (
                        <CollectionsTab onAddAssets={startAddingAssets} initialSelectedCollectionId={focusedCollectionId} />
                    )}
                    {activeTab === "assets" && (
                        <AssetsTab pickingForCollectionId={focusedCollectionId} onDonePicking={finishAddingAssets} />
                    )}
                </div>
            </Panel>
        </div>
    );
};
