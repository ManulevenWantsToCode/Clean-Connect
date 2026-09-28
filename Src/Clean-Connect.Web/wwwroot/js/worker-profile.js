/* ==========================================================================
   Clean Connect - Worker Profile Wizard & Geolocation Script
   Production-ready validation, location detection, live summary & smooth UX
   ========================================================================== */

document.addEventListener("DOMContentLoaded", function () {
    let currentStep = 1;
    const totalSteps = 3;

    const steps = document.querySelectorAll(".wizard-step");
    const stepIndicators = document.querySelectorAll(".profile-stepper .step-item");
    const connectors = document.querySelectorAll(".profile-stepper .step-connector");
    const form = document.getElementById("workerProfileForm");
    const saveButton = document.getElementById("saveButton");

    // ==========================================
    // STEP NAVIGATION & ANIMATION
    // ==========================================
    function updateStepper(step) {
        stepIndicators.forEach(function (item, index) {
            item.classList.remove("active", "completed");
            const stepNum = index + 1;
            const circle = item.querySelector(".step-circle");

            if (stepNum < step) {
                item.classList.add("completed");
                if (circle) {
                    circle.innerHTML = '<i class="fa-solid fa-check"></i>';
                }
            } else if (stepNum === step) {
                item.classList.add("active");
                if (circle) {
                    circle.innerHTML = stepNum;
                }
            } else {
                if (circle) {
                    circle.innerHTML = stepNum;
                }
            }
        });

        connectors.forEach(function (connector, index) {
            if (index + 1 < step) {
                connector.classList.add("completed");
            } else {
                connector.classList.remove("completed");
            }
        });
    }

    function showStep(step) {
        steps.forEach(function (s) {
            s.style.display = "none";
            s.classList.remove("active");
        });

        const target = document.getElementById("step" + step);
        if (target) {
            target.style.display = "block";
            target.classList.add("active");
        }

        currentStep = step;
        updateStepper(step);

        if (step === 3) {
            updateStep3Summary();
        }

        window.scrollTo({
            top: target ? target.offsetTop - 120 : 0,
            behavior: "smooth"
        });
    }

    // ==========================================
    // STEP VALIDATION
    // ==========================================
    function clearErrors() {
        document.querySelectorAll(".step-error").forEach(function (el) {
            el.remove();
        });
        document.querySelectorAll(".input-validation-error").forEach(function (el) {
            el.classList.remove("input-validation-error");
        });
        document.querySelectorAll(".custom-floating-box.has-error").forEach(function (el) {
            el.classList.remove("has-error");
        });
    }

    function showError(field, message) {
        field.classList.add("input-validation-error");
        const container = field.closest(".custom-floating-box") || field.parentElement;
        if (container) {
            container.classList.add("has-error");
        }

        const group = field.closest(".form-field-group") || field.parentElement;
        const small = document.createElement("small");
        small.className = "text-danger step-error d-flex align-items-center gap-1 mt-1";
        small.innerHTML = '<i class="fa-solid fa-circle-exclamation"></i> ' + message;

        if (group) {
            group.appendChild(small);
        } else {
            field.parentElement.appendChild(small);
        }
    }

    function validateStep(step) {
        clearErrors();
        let isValid = true;
        const currentStepEl = document.getElementById("step" + step);
        if (!currentStepEl) return true;

        const fields = currentStepEl.querySelectorAll("input:not([type=hidden]), select, textarea");

        fields.forEach(function (field) {
            if (field.disabled || field.readOnly) return;
            const value = field.value.trim();

            // Required check
            if (value === "") {
                isValid = false;
                const label = field.closest(".form-field-group")?.querySelector(".form-field-label")?.innerText?.replace(/\*|Optional/gi, "").trim() ||
                              field.closest(".form-floating")?.querySelector("label")?.innerText ||
                              field.getAttribute("placeholder") || "This field";
                showError(field, label + " is required.");
                return;
            }

            // Email format
            if (field.name === "Email" && value !== "") {
                const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
                if (!emailRegex.test(value)) {
                    isValid = false;
                    showError(field, "Enter a valid email address.");
                }
            }

            // Contact / Phone format
            if (field.name === "Contact" && value !== "") {
                const phoneClean = value.replace(/[\s-]/g, "");
                if (phoneClean.length < 11 || phoneClean.length > 15) {
                    isValid = false;
                    showError(field, "Phone number must be between 11 and 15 digits.");
                }
            }

            // DOB check
            if (field.name === "Dob" && value !== "") {
                const dobDate = new Date(value);
                const today = new Date();
                if (dobDate >= today) {
                    isValid = false;
                    showError(field, "Date of birth cannot be today or in the future.");
                }
            }

            // ServiceTypeId check
            if (field.name === "ServiceTypeId" && (!value || value === "")) {
                isValid = false;
                showError(field, "Please select a cleaning service category.");
            }

            // HourlyRate check
            if (field.name === "HourlyRate" && value !== "") {
                const rate = parseFloat(value);
                if (isNaN(rate) || rate <= 0 || rate > 1000000) {
                    isValid = false;
                    showError(field, "Hourly rate must be between ₦1 and ₦1,000,000.");
                }
            }
        });

        return isValid;
    }

    // Navigation buttons
    const nextBtn1 = document.getElementById("nextBtn1");
    if (nextBtn1) {
        nextBtn1.addEventListener("click", function () {
            if (validateStep(1)) {
                showStep(2);
            }
        });
    }

    const nextBtn2 = document.getElementById("nextBtn2");
    if (nextBtn2) {
        nextBtn2.addEventListener("click", function () {
            if (validateStep(2)) {
                showStep(3);
            }
        });
    }

    const prevBtn2 = document.getElementById("prevBtn2");
    if (prevBtn2) {
        prevBtn2.addEventListener("click", function () {
            showStep(1);
        });
    }

    const prevBtn3 = document.getElementById("prevBtn3");
    if (prevBtn3) {
        prevBtn3.addEventListener("click", function () {
            showStep(2);
        });
    }

    // Allow clicking completed steps in stepper
    stepIndicators.forEach(function (item, index) {
        item.style.cursor = "pointer";
        item.addEventListener("click", function () {
            const targetStep = index + 1;
            if (targetStep < currentStep) {
                showStep(targetStep);
            } else if (targetStep > currentStep) {
                if (validateStep(currentStep)) {
                    showStep(targetStep);
                }
            }
        });
    });

    // ==========================================
    // GEOLOCATION DETECTION & MAP
    // ==========================================
    const detectBtn = document.getElementById("detectLocationBtn");
    const latInput = document.getElementById("latitude");
    const lngInput = document.getElementById("longitude");
    const stateInput = document.getElementById("state");
    const latPreview = document.getElementById("latPreview");
    const lngPreview = document.getElementById("lngPreview");
    const statePreview = document.getElementById("statePreview");
    const mapFrame = document.getElementById("mapFrame");
    const mapPlaceholder = document.getElementById("mapPlaceholder");

    function updateMapDisplay(lat, lng) {
        if (mapFrame) {
            mapFrame.src = `https://maps.google.com/maps?q=${lat},${lng}&z=15&output=embed`;
            mapFrame.style.display = "block";
            mapFrame.style.opacity = "1";
        }
        if (mapPlaceholder) {
            mapPlaceholder.style.display = "none";
        }
    }

    // Check if initial coordinates exist
    if (latInput && lngInput && latInput.value && lngInput.value) {
        const initialLat = parseFloat(latInput.value);
        const initialLng = parseFloat(lngInput.value);
        if (!isNaN(initialLat) && !isNaN(initialLng)) {
            if (latPreview) latPreview.innerText = initialLat.toFixed(6);
            if (lngPreview) lngPreview.innerText = initialLng.toFixed(6);
            if (stateInput && statePreview) {
                statePreview.innerText = stateInput.value || "Detected";
            }
            updateMapDisplay(initialLat, initialLng);
        }
    }

    if (detectBtn) {
        detectBtn.addEventListener("click", async function () {
            if (!navigator.geolocation) {
                showToast("Geolocation is not supported by your browser.", "danger");
                return;
            }

            detectBtn.disabled = true;
            detectBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Locating GPS...';

            navigator.geolocation.getCurrentPosition(
                async function (position) {
                    const lat = position.coords.latitude;
                    const lng = position.coords.longitude;

                    if (latInput) latInput.value = lat.toFixed(6);
                    if (lngInput) lngInput.value = lng.toFixed(6);
                    if (latPreview) latPreview.innerText = lat.toFixed(6);
                    if (lngPreview) lngPreview.innerText = lng.toFixed(6);

                    updateMapDisplay(lat, lng);

                    // Reverse geocode
                    try {
                        const response = await fetch(
                            `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${lat}&lon=${lng}`
                        );
                        const result = await response.json();
                        let state = "";
                        if (result.address) {
                            state = result.address.state || result.address.county || result.address.city || "";
                        }
                        if (stateInput) stateInput.value = state;
                        if (statePreview) statePreview.innerText = state || "Detected";
                    } catch {
                        if (statePreview) statePreview.innerText = "Location Found";
                    }

                    detectBtn.classList.add("btn-success");
                    detectBtn.innerHTML = '<i class="fa-solid fa-circle-check me-2"></i>Location Locked';
                    detectBtn.disabled = false;
                    showToast("GPS location detected successfully.", "success");
                },
                function (error) {
                    detectBtn.disabled = false;
                    detectBtn.classList.remove("btn-success");
                    detectBtn.innerHTML = '<i class="fa-solid fa-location-crosshairs me-2"></i>Use My Current Location';

                    let msg = "Unable to retrieve location.";
                    if (error.code === error.PERMISSION_DENIED) msg = "Location permission denied.";
                    else if (error.code === error.POSITION_UNAVAILABLE) msg = "Location unavailable.";
                    else if (error.code === error.TIMEOUT) msg = "Location request timed out.";

                    showToast(msg, error.code === error.PERMISSION_DENIED ? "danger" : "warning");
                },
                { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }
            );
        });
    }

    if (stateInput && statePreview) {
        stateInput.addEventListener("input", function () {
            statePreview.innerText = stateInput.value.trim() || "Not Detected";
        });
    }

    // ==========================================
    // STEP 3 DYNAMIC SUMMARY
    // ==========================================
    function updateStep3Summary() {
        const firstName = document.getElementById("FirstName")?.value || "";
        const lastName = document.getElementById("LastName")?.value || "";
        const email = document.getElementById("Email")?.value || "";
        const contact = document.getElementById("Contact")?.value || "";
        const state = document.getElementById("state")?.value || "";
        const lat = document.getElementById("latitude")?.value || "";
        const lng = document.getElementById("longitude")?.value || "";
        const serviceSelect = document.getElementById("ServiceTypeId");
        const rateInput = document.getElementById("HourlyRate");

        let serviceName = "Select a category above";
        if (serviceSelect && serviceSelect.selectedIndex > 0) {
            serviceName = serviceSelect.options[serviceSelect.selectedIndex].text;
        }

        let rateText = "Set rate above";
        if (rateInput && rateInput.value) {
            const rate = parseFloat(rateInput.value);
            if (!isNaN(rate) && rate > 0) {
                rateText = "₦" + rate.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 }) + " /hr";
            }
        }

        const summaryName = document.getElementById("summaryFullName");
        const summaryContact = document.getElementById("summaryContact");
        const summaryLocation = document.getElementById("summaryLocation");
        const summaryService = document.getElementById("summaryService");
        const summaryRate = document.getElementById("summaryRate");

        if (summaryName) {
            summaryName.innerText = (firstName + " " + lastName).trim() || "Worker Candidate";
        }
        if (summaryContact) {
            summaryContact.innerText = [email, contact].filter(Boolean).join(" • ") || "Pending Details";
        }
        if (summaryLocation) {
            let locText = state || "State not set";
            if (lat && lng) {
                locText += ` (${parseFloat(lat).toFixed(4)}, ${parseFloat(lng).toFixed(4)})`;
            }
            summaryLocation.innerText = locText;
        }
        if (summaryService) {
            summaryService.innerText = serviceName;
        }
        if (summaryRate) {
            summaryRate.innerText = rateText;
        }
    }

    const serviceTypeSelect = document.getElementById("ServiceTypeId");
    if (serviceTypeSelect) {
        serviceTypeSelect.addEventListener("change", function () {
            updateStep3Summary();
        });
    }

    const hourlyRateInput = document.getElementById("HourlyRate");
    if (hourlyRateInput) {
        hourlyRateInput.addEventListener("input", function () {
            updateStep3Summary();
        });
    }

    // ==========================================
    // FORM SUBMIT
    // ==========================================
    if (form) {
        form.addEventListener("submit", function (e) {
            if (!validateStep(3)) {
                e.preventDefault();
                showStep(3);
                showToast("Please choose your service specialization before submitting.", "danger");
                return;
            }

            if (saveButton) {
                saveButton.disabled = true;
                saveButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Creating Worker Profile...';
            }
        });
    }

    // ==========================================
    // SERVER-SIDE VALIDATION RECOVERY
    // ==========================================
    const step3Errors = document.querySelector("#step3 .field-validation-error:not(:empty)");
    const step2Errors = document.querySelector("#step2 .field-validation-error:not(:empty)");
    const step1Errors = document.querySelector("#step1 .field-validation-error:not(:empty)");

    if (step3Errors) {
        showStep(3);
    } else if (step2Errors && !step1Errors) {
        showStep(2);
    } else {
        showStep(1);
    }

    // ==========================================
    // TOAST NOTIFICATION UTILITY
    // ==========================================
    window.showToast = function (message, type = "success") {
        let toast = document.getElementById("workerProfileToast");
        if (!toast) {
            toast = document.createElement("div");
            toast.id = "workerProfileToast";
            toast.className = "profile-toast-notice";
            document.body.appendChild(toast);
        }

        const colors = {
            success: "linear-gradient(135deg, #059669 0%, #10b981 100%)",
            danger: "linear-gradient(135deg, #dc2626 0%, #ef4444 100%)",
            warning: "linear-gradient(135deg, #d97706 0%, #f59e0b 100%)"
        };
        const icons = {
            success: '<i class="fa-solid fa-circle-check"></i>',
            danger: '<i class="fa-solid fa-circle-exclamation"></i>',
            warning: '<i class="fa-solid fa-triangle-exclamation"></i>'
        };

        toast.style.background = colors[type] || colors.success;
        toast.innerHTML = (icons[type] || icons.success) + "<span>" + message + "</span>";
        toast.classList.add("is-active");

        setTimeout(function () {
            toast.classList.remove("is-active");
        }, 3600);
    };
});