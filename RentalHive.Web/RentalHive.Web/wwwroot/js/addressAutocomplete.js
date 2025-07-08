// This function initializes the Google Places Autocomplete on a given input element.
window.initAddressAutocomplete = (elementId, dotNetHelper) => {
    const input = document.getElementById(elementId);
    if (!input) {
        console.error("Address input element not found: " + elementId);
        return;
    }

    // Restrict search to Sweden
    const options = {
        componentRestrictions: { country: "se" },
        fields: ["formatted_address"], // Only fetch the formatted address
        types: ["address"],
    };

    const autocomplete = new google.maps.places.Autocomplete(input, options);

    // Add a listener for when the user selects an address from the dropdown.
    autocomplete.addListener("place_changed", () => {
        const place = autocomplete.getPlace();
        if (place && place.formatted_address) {
            // Call the C# method in our Blazor component to update the address.
            dotNetHelper.invokeMethodAsync("UpdateAddress", place.formatted_address);
        }
    });
};

// A dummy initMap function required by the Google Maps script callback.
// It doesn't need to do anything for our purpose.
function initMap() { }
