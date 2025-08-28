window.triageMap = {
    map: null,
    layer: null,
    init: function (id) {
        //if (this.map) return;
        this.map = L.map(id).setView([29.7, -95.3], 4);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 18 }).addTo(this.map);

        // Use FeatureGroup so we can call getBounds()
        this.layer = L.featureGroup().addTo(this.map);
    },
    setIncidents: function (geojsonList) {
        if (!this.map) return;

        this.layer.clearLayers();
        const bounds = L.latLngBounds();

        geojsonList.forEach(g => {
            try {
                const feature = JSON.parse(g);
                const geo = L.geoJSON(feature).bindPopup(function (layer) {
                    return layer.feature.properties.source + "\n" + layer.feature.properties.title;
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
