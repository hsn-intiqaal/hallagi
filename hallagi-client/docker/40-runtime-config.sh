#!/bin/sh
# Writes the runtime configuration of this deployment from environment variables,
# so one image serves any client / brand / domain without rebuilding.
#   API_URL               default "/api" (same origin through this nginx)
#   APP_NAME, APP_SHORT_NAME, APP_TAGLINE
#   APP_PRIMARY_COLOR, APP_SECONDARY_COLOR   (#RRGGBB)
#   APP_LOGO_URL, APP_SUPPORT_EMAIL, APP_WEBSITE
#   DEFAULT_TIME_ZONE     default "Asia/Riyadh"
set -eu

ROOT=/usr/share/nginx/html

# JSON string or null; escapes backslashes and quotes
json() {
  if [ -z "${1:-}" ]; then printf 'null'; else printf '"%s"' "$(printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g')"; fi
}

NAME="${APP_NAME:-حلاقي}"
SHORT="${APP_SHORT_NAME:-$NAME}"
TAGLINE="${APP_TAGLINE:-محلك في يدك}"
SECONDARY="${APP_SECONDARY_COLOR:-#173653}"

cat > "$ROOT/config.json" <<JSON
{
  "apiUrl": $(json "${API_URL:-/api}"),
  "defaultTimeZone": $(json "${DEFAULT_TIME_ZONE:-Asia/Riyadh}"),
  "brand": {
    "name": $(json "$NAME"),
    "shortName": $(json "$SHORT"),
    "tagline": $(json "$TAGLINE"),
    "primaryColor": $(json "${APP_PRIMARY_COLOR:-#2563EB}"),
    "secondaryColor": $(json "$SECONDARY"),
    "logoUrl": $(json "${APP_LOGO_URL:-}"),
    "supportEmail": $(json "${APP_SUPPORT_EMAIL:-}"),
    "website": $(json "${APP_WEBSITE:-}")
  }
}
JSON

# The install name / colors in the manifest and the first-paint title follow the brand too.
# Only the top-level keys are rewritten (the first match of each); values are JSON-escaped.
M_NAME="$(json "$NAME — $TAGLINE")" M_SHORT="$(json "$SHORT")" M_COLOR="$(json "$SECONDARY")" \
awk '
  function swap(key, value,   pat, i, rest) {
    pat = "\"" key "\": \""
    i = index($0, pat)
    if (i == 0 || (key in done)) return
    rest = substr($0, i + length(pat))
    $0 = substr($0, 1, i - 1) "\"" key "\": " value substr(rest, index(rest, "\"") + 1)
    done[key] = 1
  }
  { swap("name", ENVIRON["M_NAME"]); swap("short_name", ENVIRON["M_SHORT"]);
    swap("theme_color", ENVIRON["M_COLOR"]); swap("background_color", ENVIRON["M_COLOR"]); print }
' "$ROOT/manifest.webmanifest" > "$ROOT/manifest.tmp" && mv "$ROOT/manifest.tmp" "$ROOT/manifest.webmanifest"
# html: escape text for HTML; esc: escape a value for a sed replacement
html() { printf '%s' "$1" | sed 's/&/\&amp;/g; s/</\&lt;/g; s/>/\&gt;/g; s/"/\&quot;/g'; }
esc() { printf '%s' "$1" | sed 's/[&|\\]/\\&/g'; }
sed -i \
  -e "s|<title>[^<]*</title>|<title>$(esc "$(html "$NAME")")</title>|" \
  -e "s|<meta name=\"theme-color\" content=\"[^\"]*\">|<meta name=\"theme-color\" content=\"$(esc "$SECONDARY")\">|" \
  "$ROOT/index.html"

# An API on another origin must be allowed by the Content-Security-Policy.
API_ORIGIN=""
case "${API_URL:-/api}" in
  http://*|https://*) API_ORIGIN="$(printf '%s' "$API_URL" | sed -E 's|^(https?://[^/]+).*|\1|')" ;;
esac
CONNECT_SRC="'self'${API_ORIGIN:+ $API_ORIGIN}"
sed "s|\${CSP_CONNECT_SRC}|$(esc "$CONNECT_SRC")|" /etc/nginx/templates-static/security.conf > /etc/nginx/conf.d/security.inc

echo "runtime config written for \"$NAME\" (api: ${API_URL:-/api})"
