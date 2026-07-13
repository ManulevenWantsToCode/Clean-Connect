// ==========================================
// WORKER PROFILE WIZARD
// ==========================================

document.addEventListener("DOMContentLoaded", function () {

    let currentStep = 1;

    const steps = document.querySelectorAll(".wizard-step");
    const indicators = document.querySelectorAll(".worker-stepper .step");

    function showStep(step) {

        steps.forEach(s => {

            s.style.display = "none";

            s.classList.remove("active");

        });

        document.getElementById("step" + step).style.display = "block";
        document.getElementById("step" + step).classList.add("active");

        indicators.forEach((indicator, index) => {

            indicator.classList.remove("active");

            if (index < step) {

                indicator.classList.add("active");

            }

        });

        currentStep = step;

        window.scrollTo({
            top: 0,
            behavior: "smooth"
        });

    }

    showStep(1);

    // ==========================
    // NEXT BUTTONS
    // ==========================

    const next1 = document.getElementById("nextBtn1");

    if (next1) {

        next1.addEventListener("click", () => {

            showStep(2);

        });

    }

    const next2 = document.getElementById("nextBtn2");

    if (next2) {

        next2.addEventListener("click", () => {

            showStep(3);

        });

    }

    // ==========================
    // PREVIOUS BUTTONS
    // ==========================

    const prev2 = document.getElementById("prevBtn2");

    if (prev2) {

        prev2.addEventListener("click", () => {

            showStep(1);

            // ==========================================
            // GEOLOCATION
            // ==========================================

            const detectLocationBtn = document.getElementById("detectLocationBtn");

            if (detectLocationBtn) {

                detectLocationBtn.addEventListener("click", function () {

                    if (!navigator.geolocation) {

                        alert("Your browser doesn't support Geolocation.");

                        return;

                    }

                    detectLocationBtn.disabled = true;

                    detectLocationBtn.innerHTML =
                        '<span class="spinner-border spinner-border-sm me-2"></span>Detecting...';

                    navigator.geolocation.getCurrentPosition(

                        function (position) {

                            document.getElementById("latitude").value =
                                position.coords.latitude.toFixed(6);

                            document.getElementById("longitude").value =
                                position.coords.longitude.toFixed(6);

                            detectLocationBtn.disabled = false;

                            detectLocationBtn.innerHTML =
                                '<i class="fa-solid fa-check me-2"></i>Location Found';

                        },

                        function (error) {

                            detectLocationBtn.disabled = false;

                            detectLocationBtn.innerHTML =
                                '<i class="fa-solid fa-location-crosshairs me-2"></i>Use My Current Location';

                            switch (error.code) {

                                case error.PERMISSION_DENIED:
                                    alert("Location permission denied.");
                                    break;

                                case error.POSITION_UNAVAILABLE:
                                    alert("Location unavailable.");
                                    break;

                                case error.TIMEOUT:
                                    alert("Location request timed out.");
                                    break;

                                default:
                                    alert("Unable to retrieve location.");
                                    break;
                            }

                        }

                    );

                });

            }

        });

    }

    const prev3 = document.getElementById("prevBtn3");

    if (prev3) {

        prev3.addEventListener("click", () => {

            showStep(2);

        });

    }

});

const form = document.getElementById("workerProfileForm");

if (form) {

    form.addEventListener("submit", function () {

        const btn = document.getElementById("saveButton");

        btn.disabled = true;

        btn.innerHTML =
            '<span class="spinner-border spinner-border-sm me-2"></span>Creating Profile...';

    });

}