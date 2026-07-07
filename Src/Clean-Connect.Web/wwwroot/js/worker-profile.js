// ==========================================
// Worker Profile JavaScript
// ==========================================

document.addEventListener("DOMContentLoaded", () => {

    imagePreview();

    setupLocationButton();

    animateCards();

    setupWizard();

    setupFormSubmit();

});

// ==========================================
// PROFILE IMAGE PREVIEW
// ==========================================

function imagePreview() {

    const imageInput = document.getElementById("profileImage");
    const preview = document.getElementById("previewImage");

    if (!imageInput || !preview)
        return;

    imageInput.addEventListener("change", function () {

        const file = this.files[0];

        if (!file)
            return;

        const reader = new FileReader();

        reader.onload = function (e) {

            preview.src = e.target.result;

        };

        reader.readAsDataURL(file);

    });

}

// ==========================================
// GET CURRENT LOCATION
// ==========================================

function setupLocationButton() {

    const button = document.querySelector(".btn-location");

    if (!button)
        return;

    button.addEventListener("click", getCurrentLocation);

}

function getCurrentLocation() {

    if (!navigator.geolocation) {

        alert("Your browser does not support Geolocation.");

        return;

    }

    const latitude = document.getElementById("latitude");
    const longitude = document.getElementById("longitude");

    const button = document.querySelector(".btn-location");

    button.disabled = true;

    button.innerHTML =
        '<span class="spinner-border spinner-border-sm me-2"></span>Getting Location...';

    navigator.geolocation.getCurrentPosition(

        function (position) {

            latitude.value = position.coords.latitude.toFixed(6);

            longitude.value = position.coords.longitude.toFixed(6);

            button.disabled = false;

            button.innerHTML =
                '<i class="fa-solid fa-location-crosshairs me-2"></i>Use My Current Location';

        },

        function () {

            alert("Unable to retrieve your location.");

            button.disabled = false;

            button.innerHTML =
                '<i class="fa-solid fa-location-crosshairs me-2"></i>Use My Current Location';

        }

    );

}

// ==========================================
// WIZARD
// ==========================================

function setupWizard() {

    const steps = document.querySelectorAll(".wizard-step");

    const progressSteps = document.querySelectorAll(".worker-stepper .step");

    let currentStep = 0;

    function showStep(index) {

        steps.forEach(step => {

            step.classList.remove("active");

        });

        steps[index].classList.add("active");

        progressSteps.forEach((step, i) => {

            if (i <= index) {

                step.classList.add("active");

            }
            else {

                step.classList.remove("active");

            }

        });

        window.scrollTo({

            top: 0,

            behavior: "smooth"

        });

    }

    // NEXT

    const nextBtn1 = document.getElementById("nextBtn1");

    if (nextBtn1) {

        nextBtn1.addEventListener("click", () => {

            currentStep = 1;

            showStep(currentStep);

        });

    }

    const nextBtn2 = document.getElementById("nextBtn2");

    if (nextBtn2) {

        nextBtn2.addEventListener("click", () => {

            currentStep = 2;

            showStep(currentStep);

        });

    }

    // PREVIOUS

    const prevBtn2 = document.getElementById("prevBtn2");

    if (prevBtn2) {

        prevBtn2.addEventListener("click", () => {

            currentStep = 0;

            showStep(currentStep);

        });

    }

    const prevBtn3 = document.getElementById("prevBtn3");

    if (prevBtn3) {

        prevBtn3.addEventListener("click", () => {

            currentStep = 1;

            showStep(currentStep);

        });

    }

    showStep(currentStep);

}

// ==========================================
// CARD ANIMATION
// ==========================================

function animateCards() {

    const cards = document.querySelectorAll(".worker-card");

    if (!("IntersectionObserver" in window))
        return;

    const observer = new IntersectionObserver((entries) => {

        entries.forEach(entry => {

            if (entry.isIntersecting) {

                entry.target.classList.add("show-card");

            }

        });

    }, {

        threshold: .15

    });

    cards.forEach(card => {

        observer.observe(card);

    });

}

// ==========================================
// SAVE BUTTON
// ==========================================

function setupFormSubmit() {

    const form = document.getElementById("workerProfileForm");

    const saveButton = document.getElementById("saveButton");

    if (!form || !saveButton)
        return;

    form.addEventListener("submit", function () {

        saveButton.disabled = true;

        saveButton.innerHTML =
            '<span class="spinner-border spinner-border-sm me-2"></span>Creating Profile...';

    });

}