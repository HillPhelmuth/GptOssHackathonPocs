let _dotnetRef = null;

window.triageMap = {
    map: null,
    layer: null,
    dotnetRef: null,
    init: function (id, dotnet) {
        this.dotnetRef = dotnet;
        _dotnetRef = dotnet;
        this.map = L.map(id).setView([29.7, -95.3], 5);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 18 }).addTo(this.map);

        // Use FeatureGroup so we can call getBounds()
        this.layer = L.featureGroup().addTo(this.map);
    },
    escapeHtml: function (str) {
        return String(str ?? "").replace(/[&<>"']/g, function (m) {
            switch (m) {
                case '&': return '&amp;';
                case '<': return '&lt;';
                case '>': return '&gt;';
                case '"': return '&quot;';
                case "'": return '&#39;';
                default: return m;
            }
        });
    },
    onIncidentClick: function (id) {
        try {
            if (_dotnetRef) {
                _dotnetRef.invokeMethodAsync('OnIncidentSelected', id);
            }
        } catch (e) {
            console.error('Failed to invoke OnIncidentSelected', e);
        }
    },
    setIncidents: function (geojsonList) {
        if (!this.map) return;
        this.layer.clearLayers();
        const bounds = L.latLngBounds();

        geojsonList.forEach(g => {
            try {
                const feature = JSON.parse(g);
                const self = this;
                const geo = L.geoJSON(feature).bindPopup(function (layer) {
                    const p = layer.feature?.properties ?? {};
                    const src = self.escapeHtml(p.source);
                    const title = self.escapeHtml(p.title);
                    const id = self.escapeHtml(p.id);
                    return `
<div class="incident-popup">
  <div><strong>${src}</strong></div>
  <div>${title}</div>
  <div style="margin-top:.5rem">
    <button type="button" class="incident-select" onclick="triageMap.onIncidentClick('${id}')">View details</button>
  </div>
</div>`;
                }); // L.GeoJSON extends FeatureGroup
                geo.addTo(this.layer);

                // Safely extend bounds if the layer supports getBounds()
                if (typeof geo.getBounds === "function") {
                    bounds.extend(geo.getBounds());
                }
            } catch {
                // ignore bad geometry
            }
        });

        if (bounds.isValid()) {
            this.map.fitBounds(bounds.pad(0.2));
        }
    }
};
