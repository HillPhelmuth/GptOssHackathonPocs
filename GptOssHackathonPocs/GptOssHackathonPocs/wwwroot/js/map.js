let _dotnetRef = null;

window.triageMap = {
    map: null,
    layer: null,
    dotnetRef: null,
    hospitalMarker: null,
    init: function (id, dotnet) {
        this.dotnetRef = dotnet;
        _dotnetRef = dotnet;
        this.map = L.map(id).setView([29.7, -95.3], 5);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 18 }).addTo(this.map);

        // Use FeatureGroup so we can call getBounds()
        this.layer = L.featureGroup().addTo(this.map);
        this.hospitalMarker = null;
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
    clearHospital: function () {
        try {
            if (this.hospitalMarker && this.map) {
                this.map.removeLayer(this.hospitalMarker);
            }
        } catch { /* ignore */ }
        this.hospitalMarker = null;
    },
    showHospital: function (lat, lng, name, city, state, beds, type) {
        if (!this.map || typeof lat !== 'number' || typeof lng !== 'number') return;
        // Remove any previous marker first
        this.clearHospital();
        const title = this.escapeHtml(name ?? 'Hospital');
        const details = [
            city && state ? `${this.escapeHtml(city)}, ${this.escapeHtml(state)}` : null,
            type ? this.escapeHtml(type) : null,
            (typeof beds === 'number' && beds > 0) ? `${beds} beds` : null
        ].filter(Boolean).join(' • ');

        const popup = `<div class="incident-popup"><div><strong>${title}</strong></div>${details ? `<div>${details}</div>` : ''}</div>`;

        // Use a circle marker so it reads as a point of interest
        const marker = L.marker([lat, lng], {
            radius: 8,
            color: '#0d6efd',
            weight: 2,
            fillColor: '#66b0ff',
            fillOpacity: 0.8
        }).bindPopup(popup);

        marker.addTo(this.map);
        this.hospitalMarker = marker;

        // Optionally pan/zoom to make it visible
        try {
            this.map.panTo([lat, lng]);
        } catch { /* ignore */ }
    },
    setIncidents: function (geojsonList) {
        if (!this.map) return;
        this.layer.clearLayers();
        // Clear any selected hospital marker when incidents are redrawn
        this.clearHospital();
        const bounds = L.latLngBounds();

        geojsonList.forEach(g => {
            try {
                const feature = JSON.parse(g);
                const self = this;
                const geo = L.geoJSON(feature, {
                    style: function (feature) {
                        return { color: feature.properties.color };
                    }
                }).bindPopup(function (layer) {
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
