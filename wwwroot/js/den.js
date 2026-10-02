// DevDen Core JS Interop Module
(function () {
    window.denStorage = {
        getItem: function (key) {
            try {
                return localStorage.getItem(key);
            } catch (e) {
                console.error("denStorage.getItem failed", e);
                return null;
            }
        },
        setItem: function (key, value) {
            try {
                localStorage.setItem(key, value);
                return true;
            } catch (e) {
                if (e.name === 'QuotaExceededError' || e.code === 22) {
                    console.warn("denStorage quota exceeded!");
                    return false;
                }
                console.error("denStorage.setItem failed", e);
                return false;
            }
        },
        removeItem: function (key) {
            try {
                localStorage.removeItem(key);
                return true;
            } catch (e) {
                console.error("denStorage.removeItem failed", e);
                return false;
            }
        },
        getKeys: function (prefix) {
            try {
                const keys = [];
                for (let i = 0; i < localStorage.length; i++) {
                    const k = localStorage.key(i);
                    if (!prefix || (k && k.startsWith(prefix))) {
                        keys.push(k);
                    }
                }
                return keys;
            } catch (e) {
                console.error("denStorage.getKeys failed", e);
                return [];
            }
        },
        clear: function () {
            try {
                localStorage.clear();
                return true;
            } catch (e) {
                return false;
            }
        }
    };

    window.denUtils = {
        copyText: async function (text) {
            try {
                if (navigator.clipboard && navigator.clipboard.writeText) {
                    await navigator.clipboard.writeText(text);
                    return true;
                } else {
                    const ta = document.createElement("textarea");
                    ta.value = text;
                    ta.style.position = "fixed";
                    ta.style.opacity = "0";
                    document.body.appendChild(ta);
                    ta.select();
                    document.execCommand("copy");
                    document.body.removeChild(ta);
                    return true;
                }
            } catch (e) {
                console.error("copyText failed", e);
                return false;
            }
        },

        focusElement: function (elementId) {
            setTimeout(function () {
                const el = document.getElementById(elementId);
                if (el) {
                    el.focus();
                    if (typeof el.select === "function") {
                        el.select();
                    }
                }
            }, 50);
        },

        downloadJson: function (fileName, jsonContent) {
            try {
                const blob = new Blob([jsonContent], { type: "application/json" });
                const url = URL.createObjectURL(blob);
                const a = document.createElement("a");
                a.href = url;
                a.download = fileName;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
                return true;
            } catch (e) {
                console.error("downloadJson failed", e);
                return false;
            }
        }
    };

    let hotkeyDotNetRef = null;

    window.denHotkeys = {
        register: function (dotNetRef) {
            hotkeyDotNetRef = dotNetRef;
        },
        unregister: function () {
            hotkeyDotNetRef = null;
        }
    };

    // Global Keydown Listener
    document.addEventListener("keydown", function (e) {
        if (!hotkeyDotNetRef) return;

        const isInput = ["INPUT", "TEXTAREA", "SELECT"].includes(document.activeElement?.tagName) ||
                        document.activeElement?.isContentEditable;

        const isCmdOrCtrl = e.metaKey || e.ctrlKey;
        const key = e.key;

        // Command Bar: Ctrl/Cmd + K
        if (isCmdOrCtrl && (key === "k" || key === "K")) {
            e.preventDefault();
            hotkeyDotNetRef.invokeMethodAsync("OnCommandBarHotkey");
            return;
        }

        // Submit form inside input: Ctrl/Cmd + Enter
        if (isCmdOrCtrl && key === "Enter") {
            hotkeyDotNetRef.invokeMethodAsync("OnSubmitFormHotkey");
            return;
        }

        // Global Escape
        if (key === "Escape") {
            hotkeyDotNetRef.invokeMethodAsync("OnEscapeHotkey");
            return;
        }

        // If currently typing in an input, suppress single-key hotkeys
        if (isInput) return;

        // Non-input hotkeys
        if (key === "c" || key === "C") {
            e.preventDefault();
            hotkeyDotNetRef.invokeMethodAsync("OnNewItemHotkey");
        } else if (key === "/") {
            e.preventDefault();
            hotkeyDotNetRef.invokeMethodAsync("OnFilterHotkey");
        } else if (key === "j" || key === "J") {
            e.preventDefault();
            hotkeyDotNetRef.invokeMethodAsync("OnNextItemHotkey");
        } else if (key === "k" || key === "K") {
            e.preventDefault();
            hotkeyDotNetRef.invokeMethodAsync("OnPrevItemHotkey");
        } else if (key === "Enter") {
            hotkeyDotNetRef.invokeMethodAsync("OnOpenItemHotkey");
        } else if (key === "[") {
            e.preventDefault();
            hotkeyDotNetRef.invokeMethodAsync("OnPrevViewHotkey");
        } else if (key === "]") {
            e.preventDefault();
            hotkeyDotNetRef.invokeMethodAsync("OnNextViewHotkey");
        } else if (key === "t" || key === "T") {
            e.preventDefault();
            hotkeyDotNetRef.invokeMethodAsync("OnToggleTodayHotkey");
        }
    });
})();
