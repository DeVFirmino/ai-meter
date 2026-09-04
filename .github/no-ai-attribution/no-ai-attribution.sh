#!/bin/sh
# no-ai-attribution.sh — deterministic guard rail against AI attribution in
# commit messages and pull request text.
#
# These repositories are a portfolio. A recruiter opens the commit history.
# "Co-Authored-By: Claude", "Generated with Claude Code" and friends must never
# reach it. A prompt instruction is not a guarantee; this script is.
#
# Usage:
#   no-ai-attribution.sh strip <commit-msg-file>
#       Removes attribution lines from a git commit message, in place.
#       Git comment lines and everything below the `# ---- >8 ----` scissors
#       line are left untouched. Removed lines are echoed to stderr.
#       Exit 0 = clean or cleaned. Exit 3 = attribution is in the SUBJECT line,
#       which cannot be removed without destroying the message: commit aborted.
#
#   no-ai-attribution.sh check <commit-msg-file>
#       Read-only verification with git commit message semantics.
#       Exit 0 = clean. Exit 1 = attribution found.
#
#   no-ai-attribution.sh scan <file|->
#       Read-only verification of plain text (PR title, PR body, release notes).
#       No git comment or scissors handling — every line is content.
#       Exit 0 = clean. Exit 1 = attribution found.
#
# Environment:
#   AI_ATTRIBUTION_PATTERNS   Override the pattern file location.
#
# Depends only on POSIX sh + awk. No Python, no Node, no network.

set -u

SELF_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd) || exit 2
PATTERNS=${AI_ATTRIBUTION_PATTERNS:-$SELF_DIR/ai-attribution-patterns.txt}

usage() {
    sed -n '4,28p' "$0" >&2
    exit 2
}

[ $# -eq 2 ] || usage
MODE=$1
TARGET=$2

case "$MODE" in
    strip | check | scan) ;;
    *) usage ;;
esac

if [ ! -r "$PATTERNS" ]; then
    printf '%s\n' "no-ai-attribution: pattern file not found: $PATTERNS" >&2
    exit 2
fi

INPUT=$TARGET
TMP_IN=
if [ "$TARGET" = "-" ]; then
    [ "$MODE" = "scan" ] || usage
    TMP_IN=$(mktemp "${TMPDIR:-/tmp}/no-ai-attribution.XXXXXX") || exit 2
    cat >"$TMP_IN"
    INPUT=$TMP_IN
elif [ ! -r "$INPUT" ]; then
    printf '%s\n' "no-ai-attribution: cannot read $INPUT" >&2
    exit 2
fi

GITMSG=1
[ "$MODE" = "scan" ] && GITMSG=0

TMP_OUT=$(mktemp "${TMPDIR:-/tmp}/no-ai-attribution.XXXXXX") || exit 2
cleanup() { rm -f "$TMP_OUT" ${TMP_IN:+"$TMP_IN"}; }
trap cleanup EXIT INT TERM

# The awk program reads the pattern file first, then the target file.
awk -v mode="$MODE" -v gitmsg="$GITMSG" -v out="$TMP_OUT" '
    NR == FNR {
        line = $0
        sub(/^[ \t]+/, "", line)
        sub(/[ \t]+$/, "", line)
        if (line == "" || substr(line, 1, 1) == "#") next
        pat[++np] = line
        next
    }
    { raw[++n] = $0 }
    END {
        cut = 0
        seen_content = 0
        removed = 0
        found = 0
        subject_hit = 0
        k = 0

        for (i = 1; i <= n; i++) {
            l = raw[i]

            if (gitmsg && !cut && l ~ /^#[ \t]*-+[ \t]*>8[ \t]*-+/) cut = 1
            if (cut) { keep[++k] = l; continue }
            if (gitmsg && substr(l, 1, 1) == "#") { keep[++k] = l; continue }

            is_subject = 0
            if (gitmsg && !seen_content && l ~ /[^ \t]/) {
                seen_content = 1
                is_subject = 1
            }

            lower = tolower(l)
            hit = 0
            for (p = 1; p <= np; p++) {
                if (lower ~ pat[p]) { hit = 1; break }
            }

            if (!hit) { keep[++k] = l; continue }

            found++
            if (mode == "strip" && !is_subject) {
                removed++
                printf "  removed line %d: %s\n", i, l > "/dev/stderr"
                continue
            }
            if (mode == "strip" && is_subject) {
                subject_hit = 1
                keep[++k] = l
                printf "  subject line %d: %s\n", i, l > "/dev/stderr"
                continue
            }
            printf "  line %d: %s\n", i, l > "/dev/stderr"
            keep[++k] = l
        }

        if (mode == "strip") {
            # Reproduce every retained line, including blank lines at the end.
            # A clean file is not replaced below, so even its final-newline
            # state remains byte-for-byte identical.
            for (i = 1; i <= k; i++) print keep[i] > out
            close(out)
            if (subject_hit) exit 3
            if (removed == 0) exit 4
            exit 0
        }

        exit (found > 0 ? 1 : 0)
    }
' "$PATTERNS" "$INPUT"
STATUS=$?

if [ "$MODE" = "strip" ]; then
    if [ "$STATUS" -eq 3 ]; then
        exit 3
    fi
    if [ "$STATUS" -eq 4 ]; then
        # Do not rewrite a clean message. Apart from being unnecessary, an awk
        # round trip would add a newline to a final unterminated line.
        exit 0
    fi
    if [ "$STATUS" -ne 0 ]; then
        exit "$STATUS"
    fi
    cat "$TMP_OUT" >"$INPUT" || exit 2
    exit 0
fi

exit "$STATUS"
