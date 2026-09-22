// ==========================================
// CLEAN CONNECT WORKER PROFILE
// PART 1
// ==========================================

document.addEventListener("DOMContentLoaded", function () {

    let currentStep = 1;

    const totalSteps = 3;

    const steps = document.querySelectorAll(".wizard-step");

    const indicators = document.querySelectorAll(".worker-stepper .step");

    // ==========================================
    // IMAGE PREVIEW
    // ==========================================

    const profileImage =
        document.getElementById("profileImage");

    const previewImage =
        document.getElementById("previewImage");

    if (profileImage) {

        profileImage.addEventListener("change", function (e) {

            const file = e.target.files[0];

            if (!file)
                return;

            if (!file.type.startsWith("image/")) {

                showToast("Please select an image.", "danger");

                return;

            }

            const reader = new FileReader();

            reader.onload = function (event) {

                previewImage.src = event.target.result;

                previewImage.style.opacity = ".2";

                setTimeout(function () {

                    previewImage.style.transition = ".4s";

                    previewImage.style.opacity = "1";

                }, 80);

            };

            reader.readAsDataURL(file);

        });

    }

    // ==========================================
    // STEP WIZARD
    // ==========================================

    function updateStepper(step) {

        indicators.forEach(function (item, index) {

            item.classList.remove("active");

            item.classList.remove("completed");

            if (index + 1 < step) {

                item.classList.add("completed");

            }
            else if (index + 1 === step) {

                item.classList.add("active");

            }

        });

    }

    function showStep(step) {

        steps.forEach(function (s) {

            s.style.display = "none";

            s.classList.remove("active");

        });

        const current =
            document.getElementById("step" + step);

        current.style.display = "block";

        current.classList.add("active");

        current.animate([

            {

                opacity: 0,

                transform: "translateY(20px)"

            },

            {

                opacity: 1,

                transform: "translateY(0)"

            }

        ],

            {

                duration: 350,

                easing: "ease"

            });

        currentStep = step;

        updateStepper(step);

        window.scrollTo({

            top: 0,

            behavior: "smooth"

        });

    }

    showStep(1);

    // ==========================================
    // STEP VALIDATION
    // ==========================================

    function getFields(step) {

        return document
            .getElementById("step" + step)
            .querySelectorAll("input,select,textarea");

    }

    function clearErrors() {

        document.querySelectorAll(".step-error")
            .forEach(function (e) {

                e.remove();

            });

        document.querySelectorAll(".input-validation-error")
            .forEach(function (e) {

                e.classList.remove("input-validation-error");

            });

    }

    function showError(field, message) {

        field.classList.add("input-validation-error");

        const floating =
            field.closest(".form-floating");

        const small =
            document.createElement("small");

        small.className =
            "text-danger step-error d-block mt-1";

        small.textContent = message;

        if (floating)

            floating.appendChild(small);

        else

            field.parentElement.appendChild(small);

    }

    function validateStep(step) {

        clearErrors();

        let valid = true;

        getFields(step).forEach(function (field) {

            if (field.disabled || field.readOnly)
                return;

            const value =
                field.value.trim();

            if (value === "") {

                valid = false;

                const label =
                    field.closest(".form-floating")
                        ?.querySelector("label")
                        ?.innerText || "Field";

                showError(field, label + " is required.");

            }

            // Email validation

            if (field.name === "Email" && value !== "") {

                const regex =
                    /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

                if (!regex.test(value)) {

                    valid = false;

                    showError(field,
                        "Enter a valid email address.");

                }

            }

            // Nigerian Phone Validation

            if (field.name === "Contact" && value !== "") {

                const regex =
                    /^0[789][01]\d{8}$/;

                if (!regex.test(value)) {

                    valid = false;

                    showError(field,
                        "Enter a valid phone number.");

                }

            }

        });

        return valid;

    }

    // ==========================================
    // NAVIGATION BUTTONS
    // ==========================================

    document.getElementById("nextBtn1")
        ?.addEventListener("click", function () {

            if (validateStep(1))

                showStep(2);

        });

    document.getElementById("nextBtn2")
        ?.addEventListener("click", function () {

            if (validateStep(2))

                showStep(3);

        });

    document.getElementById("prevBtn2")
        ?.addEventListener("click", function () {

            showStep(1);

        });

    document.getElementById("prevBtn3")
        ?.addEventListener("click", function () {

            showStep(2);

        });

// ==========================================
// PART 2 STARTS HERE
// ==========================================

    // ==========================================
    // GEOLOCATION
    // ==========================================

    const detectLocationBtn =
        document.getElementById("detectLocationBtn");

    const latitudeInput =
        document.getElementById("latitude");

    const longitudeInput =
        document.getElementById("longitude");

    const stateInput =
        document.getElementById("state");

    const latPreview =
        document.getElementById("latPreview");

    const lngPreview =
        document.getElementById("lngPreview");

    const statePreview =
        document.getElementById("statePreview");

    const mapFrame =
        document.getElementById("mapFrame");

    if (detectLocationBtn) {

        detectLocationBtn.addEventListener("click", async function () {

            if (!navigator.geolocation) {

                showToast(
                    "Geolocation is not supported by your browser.",
                    "danger"
                );

                return;

            }

            detectLocationBtn.disabled = true;

            detectLocationBtn.innerHTML =
                '<span class="spinner-border spinner-border-sm me-2"></span>Detecting Location...';

            navigator.geolocation.getCurrentPosition(

                async function (position) {

                    const latitude =
                        position.coords.latitude;

                    const longitude =
                        position.coords.longitude;

                    latitudeInput.value =
                        latitude.toFixed(6);

                    longitudeInput.value =
                        longitude.toFixed(6);

                    if (latPreview)
                        latPreview.innerText =
                            latitude.toFixed(6);

                    if (lngPreview)
                        lngPreview.innerText =
                            longitude.toFixed(6);

                    // ===========================
                    // GOOGLE MAP PREVIEW
                    // ===========================

                    if (mapFrame) {

                        mapFrame.src =
                            `https://maps.google.com/maps?q=${latitude},${longitude}&z=15&output=embed`;

                    }

                    // ===========================
                    // REVERSE GEOCODING
                    // ===========================

                    try {

                        const response =
                            await fetch(

                                `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${latitude}&lon=${longitude}`

                            );

                        const result =
                            await response.json();

                        let state = "";

                        if (result.address) {

                            state =
                                result.address.state ||
                                result.address.county ||
                                result.address.city ||
                                "";

                        }

                        stateInput.value = state;

                        if (statePreview)
                            statePreview.innerText = state;

                    }

                    catch {

                        if (statePreview)
                            statePreview.innerText =
                                "Unable to detect state";

                    }

                    detectLocationBtn.classList.remove("btn-success");

                    detectLocationBtn.classList.add("btn-primary");

                    detectLocationBtn.innerHTML =
                        '<i class="fa-solid fa-circle-check me-2"></i>Location Detected';

                    detectLocationBtn.disabled = false;

                    showToast(
                        "Location detected successfully.",
                        "success"
                    );

                },

                function (error) {

                    detectLocationBtn.disabled = false;

                    detectLocationBtn.classList.remove("btn-primary");

                    detectLocationBtn.classList.add("btn-success");

                    detectLocationBtn.innerHTML =
                        '<i class="fa-solid fa-location-crosshairs me-2"></i>Use My Current Location';

                    switch (error.code) {

                        case error.PERMISSION_DENIED:

                            showToast(
                                "Location permission denied.",
                                "danger"
                            );

                            break;

                        case error.POSITION_UNAVAILABLE:

                            showToast(
                                "Location unavailable.",
                                "warning"
                            );

                            break;

                        case error.TIMEOUT:

                            showToast(
                                "Location request timed out.",
                                "warning"
                            );

                            break;

                        default:

                            showToast(
                                "Unable to retrieve location.",
                                "danger"
                            );

                            break;

                    }

                },

                {

                    enableHighAccuracy: true,

                    timeout: 10000,

                    maximumAge: 0

                }

            );

        });

    }

    // ==========================================
    // LIVE FIELD PREVIEW
    // ==========================================

    if (stateInput) {

        stateInput.addEventListener("keyup", function () {

            if (statePreview) {

                statePreview.innerText =
                    stateInput.value || "Not Detected";

            }

        });

    }

    // ==========================================
    // AUTO SAVE TO LOCAL STORAGE
    // ==========================================

    const fields =
        document.querySelectorAll("input,select,textarea");

    fields.forEach(function (field) {

        field.addEventListener("input", function () {

            localStorage.setItem(

                field.name,

                field.value

            );

        });

    });

    // ==========================================
    // RESTORE SAVED DATA
    // ==========================================

    fields.forEach(function (field) {

        const saved =
            localStorage.getItem(field.name);

        if (saved) {

            field.value = saved;

        }

    });

// ==========================================
// PART 3 STARTS HERE
// ==========================================

    // ==========================================
    // PROFILE IMAGE ANIMATION
    // ==========================================

    if (previewImage) {

        previewImage.addEventListener("load", function () {

            previewImage.animate(

                [
                    {
                        transform: "scale(.8)",
                        opacity: .3
                    },
                    {
                        transform: "scale(1)",
                        opacity: 1
                    }
                ],

                {
                    duration: 400,
                    easing: "ease-out"
                }

            );

        });

    }

    // ==========================================
    // FORM SUBMIT
    // ==========================================

    const form =
        document.getElementById("workerProfileForm");

    if (form) {

        form.addEventListener("submit", function () {

            const saveButton =
                document.getElementById("saveButton");

            saveButton.disabled = true;

            saveButton.innerHTML =
                '<span class="spinner-border spinner-border-sm me-2"></span>Creating Profile...';

        });

    }

    // ==========================================
    // CLEAR STORAGE AFTER SUCCESS
    // ==========================================

    window.clearWorkerProfileStorage = function () {

        document
            .querySelectorAll("input,select,textarea")
            .forEach(function (field) {

                if (field.name) {

                    localStorage.removeItem(field.name);

                }

            });

    };

    // ==========================================
    // TOAST NOTIFICATION
    // ==========================================

    window.showToast = function (message, type = "success") {

        let toast =
            document.getElementById("workerToast");

        if (!toast) {

            toast = document.createElement("div");

            toast.id = "workerToast";

            toast.style.position = "fixed";
            toast.style.top = "30px";
            toast.style.right = "30px";
            toast.style.zIndex = "99999";
            toast.style.padding = "15px 20px";
            toast.style.borderRadius = "12px";
            toast.style.color = "#fff";
            toast.style.fontWeight = "600";
            toast.style.minWidth = "280px";
            toast.style.boxShadow = "0 10px 30px rgba(0,0,0,.15)";
            toast.style.transition = ".35s";
            toast.style.opacity = "0";

            document.body.appendChild(toast);

        }

        switch (type) {

            case "success":

                toast.style.background = "#10b981";

                break;

            case "danger":

                toast.style.background = "#ef4444";

                break;

            case "warning":

                toast.style.background = "#f59e0b";

                break;

            default:

                toast.style.background = "#0ea5e9";

                break;

        }

        toast.innerHTML = message;

        toast.style.opacity = "1";

        toast.style.transform = "translateY(0)";

        setTimeout(function () {

            toast.style.opacity = "0";

            toast.style.transform = "translateY(-20px)";

        }, 3000);

    };

    // ==========================================
    // RESTORE STEP IF SAVED
    // ==========================================

    const savedStep =
        parseInt(localStorage.getItem("WorkerStep"));

    if (savedStep &&
        savedStep >= 1 &&
        savedStep <= totalSteps) {

        showStep(savedStep);

    }

    document
        .querySelectorAll("#nextBtn1,#nextBtn2,#prevBtn2,#prevBtn3")
        .forEach(function (btn) {

            btn.addEventListener("click", function () {

                localStorage.setItem(
                    "WorkerStep",
                    currentStep
                );

            });

        });

    // ==========================================
    // REMOVE SAVED STEP AFTER SUCCESS
    // ==========================================

    if (form) {

        form.addEventListener("submit", function () {

            localStorage.removeItem("WorkerStep");

        });

    }

    // ==========================================
    // INPUT ANIMATION
    // ==========================================

    document
        .querySelectorAll(".form-control,.form-select")
        .forEach(function (input) {

            input.addEventListener("focus", function () {

                input.parentElement.style.transform =
                    "translateY(-2px)";

            });

            input.addEventListener("blur", function () {

                input.parentElement.style.transform =
                    "translateY(0)";

            });

        });

});