const template = createRowTemplate();
const stdErrorBadgeTemplate = createStdErrBadgeTemplate();

/**
 * Clears all log entries from the log viewer and resets the
 * row index back to 1
 */
export function clearLogs() {
    const container = document.getElementById("logContainer");
    container.textContent = '';
}

/**
 * Adds a series of log entries to the log viewer and, if appropriate
 * scrolls the log viewer to the bottom
 * @param {LogEntry[]} logEntries
 */
export function addLogEntries(logEntries) {

    const container = document.getElementById("logContainer");

    if (container) {
        const scrollingContainer = container.parentElement;
        const isScrolledToBottom = getIsScrolledToBottom(scrollingContainer);

        for (const logEntry of logEntries) {

            const rowContainer = getNewRowContainer();
            rowContainer.setAttribute("data-line-index", logEntry.lineIndex);
            rowContainer.setAttribute("data-log-id", logEntry.id);
            rowContainer.setAttribute("data-timestamp", logEntry.timestamp ?? logEntry.parentTimestamp ?? "");
            const lineRow = rowContainer.firstElementChild;
            const lineArea = lineRow.firstElementChild;
            const content = lineArea.lastElementChild;

            // logEntry.content should already be HTMLEncoded other than the <span>s produced
            // by the ANSI Control Sequence Parsing, so it should be safe to set innerHTML here
            content.innerHTML = logEntry.content;
            
            if (logEntry.type === "Error") {
                const stdErrorBadge = getStdErrorBadge();
                // If there's a timestamp, we want to put the badge after it to keep timestamps
                // aligned. If there's not, then we just put the badge at the start of the content
                const timestampSpan = content.querySelector(".timestamp");
                if (timestampSpan) {
                    timestampSpan.after(stdErrorBadge);
                } else {
                    content.prepend(stdErrorBadge);
                }
            }

            insertSorted(container, rowContainer, logEntry.timestamp, logEntry.parentId, logEntry.lineIndex);
        }

        // If we were scrolled all the way to the bottom before we added the new
        // element, then keep us scrolled to the bottom. Otherwise let the user
        // stay where they are
        if (isScrolledToBottom) {
            scrollingContainer.scrollTop = scrollingContainer.scrollHeight;
        }
    }
}

/**
 * 
 * @param {HTMLElement} container
 * @param {HTMLElement} row
 * @param {string} timestamp
 * @param {string} parentLogId
 * @param {number} lineIndex
 */
function insertSorted(container, row, timestamp, parentId, lineIndex) {

    let prior = null;

    if (parentId) {
        // If we have a parent id, then we know we're on a non-timestamped line that is part
        // of a multi-line log entry. We need to find the prior line from that entry
        prior = container.querySelector(`div[data-log-id="${parentId}"][data-line-index="${lineIndex - 1}"]`);
    } else if (timestamp) {
        // Otherwise, if we have a timestamped line, we just need to find the prior line.
        // Since the rows are always in order in the DOM, as soon as we see a timestamp
        // that is less than the one we're adding, we can insert it immediately after that
        for (let rowIndex = container.children.length - 1; rowIndex >= 0; rowIndex--) {
            const targetRow = container.children[rowIndex];
            const targetRowTimestamp = targetRow.getAttribute("data-timestamp");

            if (targetRowTimestamp && targetRowTimestamp < timestamp) {
                prior = targetRow;
                break;
            }
        }
    }

    if (prior) {
        // If we found the prior row using either method above, go ahead and insert the new row after it
        prior.after(row);
    } else {
        // If we didn't, then just append it to the end. This happens with the first entry, but
        // could also happen if the logs don't have recognized timestamps.
        container.appendChild(row);
    }
}

/**
 * Clones the row container template for use with a new log entry
 * @returns {HTMLElement}
 */
function getNewRowContainer() {
    return template.cloneNode(true);
}

/**
 * Clones the stderr badge template for use with a new log entry
 * @returns
 */
function getStdErrorBadge() {
    return stdErrorBadgeTemplate.cloneNode(true);
}

/**
 * Creates the initial row container template that will be cloned
 * for each log entry
 * @returns {HTMLElement}
 */
function createRowTemplate() {
    
    const templateString = `
        <div class="line-row-container">
            <div class="line-row">
                <span class="line-area">
                    <span class="line-number"></span>
                    <span class="content"></span>
                </span>
            </div>
        </div>
    `;
    const templateElement = document.createElement("template");
    templateElement.innerHTML = templateString.trim();
    const rowTemplate = templateElement.content.firstChild;
    return rowTemplate;
}

/**
 * Creates the initial stderr badge template that will be cloned
 * for each log entry
 * @returns {HTMLElement}
 */
function createStdErrBadgeTemplate() {
    const badge = document.createElement("fluent-badge");
    badge.setAttribute("appearance", "accent");
    badge.textContent = "stderr";
    return badge;
}

/**
 * Checks to see if the specified scrolling container is scrolled all the way
 * to the bottom
 * @param {HTMLElement} scrollingContainer
 * @returns {boolean}
 */
function getIsScrolledToBottom(scrollingContainer) {
    return scrollingContainer.scrollHeight - scrollingContainer.clientHeight <= scrollingContainer.scrollTop + 1;
}

/**
 * @typedef LogEntry
 * @prop {string} timestamp
 * @prop {string} content
 * @prop {"Default" | "Error" | "Warning"} type
 * @prop {string} id
 * @prop {string} parentId
 * @prop {number} lineIndex
 * @prop {string} parentTimestamp
 * @prop {boolean} isFirstLine
 */

// SIG // Begin signature block
// SIG // MIInvQYJKoZIhvcNAQcCoIInrjCCJ6oCAQExDzANBglg
// SIG // hkgBZQMEAgEFADB3BgorBgEEAYI3AgEEoGkwZzAyBgor
// SIG // BgEEAYI3AgEeMCQCAQEEEBDgyQbOONQRoqMAEEvTUJAC
// SIG // AQACAQACAQACAQACAQAwMTANBglghkgBZQMEAgEFAAQg
// SIG // LfVtyjYw9T3mFhiJdf1va4STHPI+gL8cnmENcCdS6VGg
// SIG // gg12MIIF9DCCA9ygAwIBAgITMwAAA68wQA5Mo00FQQAA
// SIG // AAADrzANBgkqhkiG9w0BAQsFADB+MQswCQYDVQQGEwJV
// SIG // UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
// SIG // UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBv
// SIG // cmF0aW9uMSgwJgYDVQQDEx9NaWNyb3NvZnQgQ29kZSBT
// SIG // aWduaW5nIFBDQSAyMDExMB4XDTIzMTExNjE5MDkwMFoX
// SIG // DTI0MTExNDE5MDkwMFowdDELMAkGA1UEBhMCVVMxEzAR
// SIG // BgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
// SIG // bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
// SIG // bjEeMBwGA1UEAxMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
// SIG // MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA
// SIG // zkvLNa2un9GBrYNDoRGkGv7d0PqtTBB4ViYakFbjuWpm
// SIG // F0KcvDAzzaCWJPhVgIXjz+S8cHEoHuWnp/n+UOljT3eh
// SIG // A8Rs6Lb1aTYub3tB/e0txewv2sQ3yscjYdtTBtFvEm9L
// SIG // 8Yv76K3Cxzi/Yvrdg+sr7w8y5RHn1Am0Ff8xggY1xpWC
// SIG // XFI+kQM18njQDcUqSlwBnexYfqHBhzz6YXA/S0EziYBu
// SIG // 2O2mM7R6gSyYkEOHgIGTVOGnOvvC5xBgC4KNcnQuQSRL
// SIG // iUI2CmzU8vefR6ykruyzt1rNMPI8OqWHQtSDKXU5JNqb
// SIG // k4GNjwzcwbSzOHrxuxWHq91l/vLdVDGDUwIDAQABo4IB
// SIG // czCCAW8wHwYDVR0lBBgwFgYKKwYBBAGCN0wIAQYIKwYB
// SIG // BQUHAwMwHQYDVR0OBBYEFEcccTTyBDxkjvJKs/m4AgEF
// SIG // hl7BMEUGA1UdEQQ+MDykOjA4MR4wHAYDVQQLExVNaWNy
// SIG // b3NvZnQgQ29ycG9yYXRpb24xFjAUBgNVBAUTDTIzMDAx
// SIG // Mis1MDE4MjYwHwYDVR0jBBgwFoAUSG5k5VAF04KqFzc3
// SIG // IrVtqMp1ApUwVAYDVR0fBE0wSzBJoEegRYZDaHR0cDov
// SIG // L3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9jcmwvTWlj
// SIG // Q29kU2lnUENBMjAxMV8yMDExLTA3LTA4LmNybDBhBggr
// SIG // BgEFBQcBAQRVMFMwUQYIKwYBBQUHMAKGRWh0dHA6Ly93
// SIG // d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY2VydHMvTWlj
// SIG // Q29kU2lnUENBMjAxMV8yMDExLTA3LTA4LmNydDAMBgNV
// SIG // HRMBAf8EAjAAMA0GCSqGSIb3DQEBCwUAA4ICAQCEsRbf
// SIG // 80dn60xTweOWHZoWaQdpzSaDqIvqpYHE5ZzuEMJWDdcP
// SIG // 72MGw8v6BSaJQ+a+hTCXdERnIBDPKvU4ENjgu4EBJocH
// SIG // lSe8riiZUAR+z+z4OUYqoFd3EqJyfjjOJBR2z94Dy4ss
// SIG // 7LEkHUbj2NZiFqBoPYu2OGQvEk+1oaUsnNKZ7Nl7FHtV
// SIG // 7CI2lHBru83e4IPe3glIi0XVZJT5qV6Gx/QhAFmpEVBj
// SIG // SAmDdgII4UUwuI9yiX6jJFNOEek6MoeP06LMJtbqA3Bq
// SIG // +ZWmJ033F97uVpyaiS4bj3vFI/ZBgDnMqNDtZjcA2vi4
// SIG // RRMweggd9vsHyTLpn6+nXoLy03vMeebq0C3k44pgUIEu
// SIG // PQUlJIRTe6IrN3GcjaZ6zHGuQGWgu6SyO9r7qkrEpS2p
// SIG // RjnGZjx2RmCamdAWnDdu+DmfNEPAddYjaJJ7PTnd+PGz
// SIG // G+WeH4ocWgVnm5fJFhItjj70CJjgHqt57e1FiQcyWCwB
// SIG // hKX2rGgN2UICHBF3Q/rsKOspjMw2OlGphTn2KmFl5J7c
// SIG // Qxru54A9roClLnHGCiSUYos/iwFHI/dAVXEh0S0KKfTf
// SIG // M6AC6/9bCbsD61QLcRzRIElvgCgaiMWFjOBL99pemoEl
// SIG // AHsyzG6uX93fMfas09N9YzA0/rFAKAsNDOcFbQlEHKiD
// SIG // T7mI20tVoCcmSIhJATCCB3owggVioAMCAQICCmEOkNIA
// SIG // AAAAAAMwDQYJKoZIhvcNAQELBQAwgYgxCzAJBgNVBAYT
// SIG // AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQH
// SIG // EwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
// SIG // cG9yYXRpb24xMjAwBgNVBAMTKU1pY3Jvc29mdCBSb290
// SIG // IENlcnRpZmljYXRlIEF1dGhvcml0eSAyMDExMB4XDTEx
// SIG // MDcwODIwNTkwOVoXDTI2MDcwODIxMDkwOVowfjELMAkG
// SIG // A1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAO
// SIG // BgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
// SIG // dCBDb3Jwb3JhdGlvbjEoMCYGA1UEAxMfTWljcm9zb2Z0
// SIG // IENvZGUgU2lnbmluZyBQQ0EgMjAxMTCCAiIwDQYJKoZI
// SIG // hvcNAQEBBQADggIPADCCAgoCggIBAKvw+nIQHC6t2G6q
// SIG // ghBNNLrytlghn0IbKmvpWlCquAY4GgRJun/DDB7dN2vG
// SIG // EtgL8DjCmQawyDnVARQxQtOJDXlkh36UYCRsr55JnOlo
// SIG // XtLfm1OyCizDr9mpK656Ca/XllnKYBoF6WZ26DJSJhIv
// SIG // 56sIUM+zRLdd2MQuA3WraPPLbfM6XKEW9Ea64DhkrG5k
// SIG // NXimoGMPLdNAk/jj3gcN1Vx5pUkp5w2+oBN3vpQ97/vj
// SIG // K1oQH01WKKJ6cuASOrdJXtjt7UORg9l7snuGG9k+sYxd
// SIG // 6IlPhBryoS9Z5JA7La4zWMW3Pv4y07MDPbGyr5I4ftKd
// SIG // gCz1TlaRITUlwzluZH9TupwPrRkjhMv0ugOGjfdf8NBS
// SIG // v4yUh7zAIXQlXxgotswnKDglmDlKNs98sZKuHCOnqWbs
// SIG // YR9q4ShJnV+I4iVd0yFLPlLEtVc/JAPw0XpbL9Uj43Bd
// SIG // D1FGd7P4AOG8rAKCX9vAFbO9G9RVS+c5oQ/pI0m8GLhE
// SIG // fEXkwcNyeuBy5yTfv0aZxe/CHFfbg43sTUkwp6uO3+xb
// SIG // n6/83bBm4sGXgXvt1u1L50kppxMopqd9Z4DmimJ4X7Iv
// SIG // hNdXnFy/dygo8e1twyiPLI9AN0/B4YVEicQJTMXUpUMv
// SIG // dJX3bvh4IFgsE11glZo+TzOE2rCIF96eTvSWsLxGoGyY
// SIG // 0uDWiIwLAgMBAAGjggHtMIIB6TAQBgkrBgEEAYI3FQEE
// SIG // AwIBADAdBgNVHQ4EFgQUSG5k5VAF04KqFzc3IrVtqMp1
// SIG // ApUwGQYJKwYBBAGCNxQCBAweCgBTAHUAYgBDAEEwCwYD
// SIG // VR0PBAQDAgGGMA8GA1UdEwEB/wQFMAMBAf8wHwYDVR0j
// SIG // BBgwFoAUci06AjGQQ7kUBU7h6qfHMdEjiTQwWgYDVR0f
// SIG // BFMwUTBPoE2gS4ZJaHR0cDovL2NybC5taWNyb3NvZnQu
// SIG // Y29tL3BraS9jcmwvcHJvZHVjdHMvTWljUm9vQ2VyQXV0
// SIG // MjAxMV8yMDExXzAzXzIyLmNybDBeBggrBgEFBQcBAQRS
// SIG // MFAwTgYIKwYBBQUHMAKGQmh0dHA6Ly93d3cubWljcm9z
// SIG // b2Z0LmNvbS9wa2kvY2VydHMvTWljUm9vQ2VyQXV0MjAx
// SIG // MV8yMDExXzAzXzIyLmNydDCBnwYDVR0gBIGXMIGUMIGR
// SIG // BgkrBgEEAYI3LgMwgYMwPwYIKwYBBQUHAgEWM2h0dHA6
// SIG // Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvZG9jcy9w
// SIG // cmltYXJ5Y3BzLmh0bTBABggrBgEFBQcCAjA0HjIgHQBM
// SIG // AGUAZwBhAGwAXwBwAG8AbABpAGMAeQBfAHMAdABhAHQA
// SIG // ZQBtAGUAbgB0AC4gHTANBgkqhkiG9w0BAQsFAAOCAgEA
// SIG // Z/KGpZjgVHkaLtPYdGcimwuWEeFjkplCln3SeQyQwWVf
// SIG // Liw++MNy0W2D/r4/6ArKO79HqaPzadtjvyI1pZddZYSQ
// SIG // fYtGUFXYDJJ80hpLHPM8QotS0LD9a+M+By4pm+Y9G6XU
// SIG // tR13lDni6WTJRD14eiPzE32mkHSDjfTLJgJGKsKKELuk
// SIG // qQUMm+1o+mgulaAqPyprWEljHwlpblqYluSD9MCP80Yr
// SIG // 3vw70L01724lruWvJ+3Q3fMOr5kol5hNDj0L8giJ1h/D
// SIG // Mhji8MUtzluetEk5CsYKwsatruWy2dsViFFFWDgycSca
// SIG // f7H0J/jeLDogaZiyWYlobm+nt3TDQAUGpgEqKD6CPxNN
// SIG // ZgvAs0314Y9/HG8VfUWnduVAKmWjw11SYobDHWM2l4bf
// SIG // 2vP48hahmifhzaWX0O5dY0HjWwechz4GdwbRBrF1HxS+
// SIG // YWG18NzGGwS+30HHDiju3mUv7Jf2oVyW2ADWoUa9WfOX
// SIG // pQlLSBCZgB/QACnFsZulP0V3HjXG0qKin3p6IvpIlR+r
// SIG // +0cjgPWe+L9rt0uX4ut1eBrs6jeZeRhL/9azI2h15q/6
// SIG // /IvrC4DqaTuv/DDtBEyO3991bWORPdGdVk5Pv4BXIqF4
// SIG // ETIheu9BCrE/+6jMpF3BoYibV3FWTkhFwELJm3ZbCoBI
// SIG // a/15n8G9bW1qyVJzEw16UM0xghmfMIIZmwIBATCBlTB+
// SIG // MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3Rv
// SIG // bjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWlj
// SIG // cm9zb2Z0IENvcnBvcmF0aW9uMSgwJgYDVQQDEx9NaWNy
// SIG // b3NvZnQgQ29kZSBTaWduaW5nIFBDQSAyMDExAhMzAAAD
// SIG // rzBADkyjTQVBAAAAAAOvMA0GCWCGSAFlAwQCAQUAoIGu
// SIG // MBkGCSqGSIb3DQEJAzEMBgorBgEEAYI3AgEEMBwGCisG
// SIG // AQQBgjcCAQsxDjAMBgorBgEEAYI3AgEVMC8GCSqGSIb3
// SIG // DQEJBDEiBCA36GGCObR58XkCFGYrUjUyJy6GXKA+PdK3
// SIG // o7nGOXKg5TBCBgorBgEEAYI3AgEMMTQwMqAUgBIATQBp
// SIG // AGMAcgBvAHMAbwBmAHShGoAYaHR0cDovL3d3dy5taWNy
// SIG // b3NvZnQuY29tMA0GCSqGSIb3DQEBAQUABIIBAAcAUoHp
// SIG // y8n8MvrIjOuv0Lg2yzIEMiuRHrepeLS62LEyBJev0zmG
// SIG // vFw/012heiSoc+gYZ8TXsbOPpcPJ18IoRgHopKfMb0d2
// SIG // b9wuoHevYPb69YpFCRGLzkIGCaofuRwgSyhOUKrKwdyZ
// SIG // n4W4Ed+TRS4zDRbxmI4OzE6znR75R3jB2EzSTkbLC9k2
// SIG // 89kjmkOcwk+chFRSx/siQCysWfAaCWjvFvo33MQk0EKP
// SIG // JT/w2NFc2tLwbiUWKVVAi0io1RMlDg1H5qx/2kN+2v1k
// SIG // 7xuGfqovaAI7j6HhKsrardwgCAi4vRisNfnKIw1qXPuj
// SIG // twKJWRWO9WeZLh+MJnYBQN2eWqGhghcpMIIXJQYKKwYB
// SIG // BAGCNwMDATGCFxUwghcRBgkqhkiG9w0BBwKgghcCMIIW
// SIG // /gIBAzEPMA0GCWCGSAFlAwQCAQUAMIIBWQYLKoZIhvcN
// SIG // AQkQAQSgggFIBIIBRDCCAUACAQEGCisGAQQBhFkKAwEw
// SIG // MTANBglghkgBZQMEAgEFAAQgNFY/FtD8GNPkiRNo0J7P
// SIG // Dpap4LwOAeZI/VttVGu9pIECBmVd7Du3LxgTMjAyMzEy
// SIG // MTkyMDE0MTkuNzA2WjAEgAIB9KCB2KSB1TCB0jELMAkG
// SIG // A1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAO
// SIG // BgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
// SIG // dCBDb3Jwb3JhdGlvbjEtMCsGA1UECxMkTWljcm9zb2Z0
// SIG // IElyZWxhbmQgT3BlcmF0aW9ucyBMaW1pdGVkMSYwJAYD
// SIG // VQQLEx1UaGFsZXMgVFNTIEVTTjoyQUQ0LTRCOTItRkEw
// SIG // MTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAg
// SIG // U2VydmljZaCCEXgwggcnMIIFD6ADAgECAhMzAAAB3p5I
// SIG // npafKEQ9AAEAAAHeMA0GCSqGSIb3DQEBCwUAMHwxCzAJ
// SIG // BgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
// SIG // DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3Nv
// SIG // ZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29m
// SIG // dCBUaW1lLVN0YW1wIFBDQSAyMDEwMB4XDTIzMTAxMjE5
// SIG // MDcxMloXDTI1MDExMDE5MDcxMlowgdIxCzAJBgNVBAYT
// SIG // AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQH
// SIG // EwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
// SIG // cG9yYXRpb24xLTArBgNVBAsTJE1pY3Jvc29mdCBJcmVs
// SIG // YW5kIE9wZXJhdGlvbnMgTGltaXRlZDEmMCQGA1UECxMd
// SIG // VGhhbGVzIFRTUyBFU046MkFENC00QjkyLUZBMDExJTAj
// SIG // BgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZp
// SIG // Y2UwggIiMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIKAoIC
// SIG // AQC0gfQchfVCA4QOsRazp4sP8bA5fLEovazgjl0kjuFT
// SIG // EI5zRgKOVR8dIoozBDB/S2NklCAZFUEtDJepEfk2oJFD
// SIG // 22hKcI4UNZqa4UYCU/45Up4nONlQwKNHp+CSOsZ16AKF
// SIG // qCskmPP0TiCnaaYYCOziW+Fx5NT97F9qTWd9iw2NZLXI
// SIG // Stf4Vsj5W5WlwB0btBN8p78K0vP23KKwDTug47srMkvc
// SIG // 1Jq/sNx9wBL0oLNkXri49qZAXH1tVDwhbnS3eyD2dkQu
// SIG // KHUHBD52Ndo8qWD50usmQLNKS6atCkRVMgdcesejlO97
// SIG // LnYhzjdephNJeiy0/TphqNEveAcYNzf92hOn1G51aHpl
// SIG // XOxZBS7pvCpGXG0O3Dh0gFhicXQr6OTrVLUXUqn/ORZJ
// SIG // QlyCJIOLJu5zPU5LVFXztJKepMe5srIA9EK8cev+aGqp
// SIG // 8Dk1izcyvgQotRu51A9abXrl70KfHxNSqU45xv9TiXno
// SIG // cCjTT4xrffFdAZqIGU3t0sQZDnjkMiwPvuR8oPy+vKXv
// SIG // g62aGT1yWhlP4gYhZi/rpfzot3fN8ywB5R0Jh/1RjQX0
// SIG // cD/osb6ocpPxHm8Ll1SWPq08n20X7ofZ9AGjIYTccYOr
// SIG // RismUuBABIg8axfZgGRMvHvK3+nZSiF+Xd2kC6PXw3Wt
// SIG // WUzsPlwHAL49vzdwy1RmZR5x5QIDAQABo4IBSTCCAUUw
// SIG // HQYDVR0OBBYEFGswJm8bHmmqYHccyvDrPp2j0BLIMB8G
// SIG // A1UdIwQYMBaAFJ+nFV0AXmJdg/Tl0mWnG1M1GelyMF8G
// SIG // A1UdHwRYMFYwVKBSoFCGTmh0dHA6Ly93d3cubWljcm9z
// SIG // b2Z0LmNvbS9wa2lvcHMvY3JsL01pY3Jvc29mdCUyMFRp
// SIG // bWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNybDBsBggr
// SIG // BgEFBQcBAQRgMF4wXAYIKwYBBQUHMAKGUGh0dHA6Ly93
// SIG // d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY2VydHMvTWlj
// SIG // cm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAo
// SIG // MSkuY3J0MAwGA1UdEwEB/wQCMAAwFgYDVR0lAQH/BAww
// SIG // CgYIKwYBBQUHAwgwDgYDVR0PAQH/BAQDAgeAMA0GCSqG
// SIG // SIb3DQEBCwUAA4ICAQDilMB7Fw2nBjr1CILORw4D7NC2
// SIG // dash0ugusHypS2g9+rWX21rdcfhjIms0rsvhrMYlR85I
// SIG // TFvhaivIK7i0Fjf7Dgl/nxlIE/S09tXESKXGY+P2RSL8
// SIG // LZAXLAs9VxFLF2DkiVD4rWOxPG25XZpoWGdvafl0KSHL
// SIG // Bv6vmI5KgVvZsNK7tTH8TE0LPTEw4g9vIAFRqzwNzcpI
// SIG // kgob3aku1V/vy3BM/VG87aP8NvFgPBzgh6gU2w0R5oj+
// SIG // zCI/kkJiPVSGsmLCBkY73pZjWtDr21PQiUs/zXzBIH9j
// SIG // RzGVGFvCqlhIyIz3xyCsVpTTGIbln1kUh2QisiADQNGi
// SIG // S+LKB0Lc82djJzX42GPOdcB2IxoMFI/4ZS0YEDuUt9Gc
// SIG // e/BqgSn8paduWjlif6j4Qvg1zNoF2oyF25fo6RnFQDcL
// SIG // RRbowiUXWW3h9UfkONRY4AYOJtzkxQxqLeQ0rlZEII5L
// SIG // u6TlT7ZXROOkJQ4P9loT6U0MVx+uLD9Rn5AMFLbeq62T
// SIG // PzwsERuoIq2Jp00Sy7InAYaGC4fhBBY1b4lwBk5OqZ7v
// SIG // I8f+Fj1rtI7M+8hc4PNvxTKgpPcCty78iwMgxzfhcWxw
// SIG // MbYMGne6C0DzNFhhEXQdbpjwiImLEn/4+/RKh3aDcEGE
// SIG // TlZvmV9dEV95+m0ZgJ7JHjYYtMJ1WnlaICzHRg/p6jCC
// SIG // B3EwggVZoAMCAQICEzMAAAAVxedrngKbSZkAAAAAABUw
// SIG // DQYJKoZIhvcNAQELBQAwgYgxCzAJBgNVBAYTAlVTMRMw
// SIG // EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRt
// SIG // b25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRp
// SIG // b24xMjAwBgNVBAMTKU1pY3Jvc29mdCBSb290IENlcnRp
// SIG // ZmljYXRlIEF1dGhvcml0eSAyMDEwMB4XDTIxMDkzMDE4
// SIG // MjIyNVoXDTMwMDkzMDE4MzIyNVowfDELMAkGA1UEBhMC
// SIG // VVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcT
// SIG // B1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jw
// SIG // b3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUt
// SIG // U3RhbXAgUENBIDIwMTAwggIiMA0GCSqGSIb3DQEBAQUA
// SIG // A4ICDwAwggIKAoICAQDk4aZM57RyIQt5osvXJHm9DtWC
// SIG // 0/3unAcH0qlsTnXIyjVX9gF/bErg4r25PhdgM/9cT8dm
// SIG // 95VTcVrifkpa/rg2Z4VGIwy1jRPPdzLAEBjoYH1qUoNE
// SIG // t6aORmsHFPPFdvWGUNzBRMhxXFExN6AKOG6N7dcP2CZT
// SIG // fDlhAnrEqv1yaa8dq6z2Nr41JmTamDu6GnszrYBbfowQ
// SIG // HJ1S/rboYiXcag/PXfT+jlPP1uyFVk3v3byNpOORj7I5
// SIG // LFGc6XBpDco2LXCOMcg1KL3jtIckw+DJj361VI/c+gVV
// SIG // mG1oO5pGve2krnopN6zL64NF50ZuyjLVwIYwXE8s4mKy
// SIG // zbnijYjklqwBSru+cakXW2dg3viSkR4dPf0gz3N9QZpG
// SIG // dc3EXzTdEonW/aUgfX782Z5F37ZyL9t9X4C626p+Nuw2
// SIG // TPYrbqgSUei/BQOj0XOmTTd0lBw0gg/wEPK3Rxjtp+iZ
// SIG // fD9M269ewvPV2HM9Q07BMzlMjgK8QmguEOqEUUbi0b1q
// SIG // GFphAXPKZ6Je1yh2AuIzGHLXpyDwwvoSCtdjbwzJNmSL
// SIG // W6CmgyFdXzB0kZSU2LlQ+QuJYfM2BjUYhEfb3BvR/bLU
// SIG // HMVr9lxSUV0S2yW6r1AFemzFER1y7435UsSFF5PAPBXb
// SIG // GjfHCBUYP3irRbb1Hode2o+eFnJpxq57t7c+auIurQID
// SIG // AQABo4IB3TCCAdkwEgYJKwYBBAGCNxUBBAUCAwEAATAj
// SIG // BgkrBgEEAYI3FQIEFgQUKqdS/mTEmr6CkTxGNSnPEP8v
// SIG // BO4wHQYDVR0OBBYEFJ+nFV0AXmJdg/Tl0mWnG1M1Gely
// SIG // MFwGA1UdIARVMFMwUQYMKwYBBAGCN0yDfQEBMEEwPwYI
// SIG // KwYBBQUHAgEWM2h0dHA6Ly93d3cubWljcm9zb2Z0LmNv
// SIG // bS9wa2lvcHMvRG9jcy9SZXBvc2l0b3J5Lmh0bTATBgNV
// SIG // HSUEDDAKBggrBgEFBQcDCDAZBgkrBgEEAYI3FAIEDB4K
// SIG // AFMAdQBiAEMAQTALBgNVHQ8EBAMCAYYwDwYDVR0TAQH/
// SIG // BAUwAwEB/zAfBgNVHSMEGDAWgBTV9lbLj+iiXGJo0T2U
// SIG // kFvXzpoYxDBWBgNVHR8ETzBNMEugSaBHhkVodHRwOi8v
// SIG // Y3JsLm1pY3Jvc29mdC5jb20vcGtpL2NybC9wcm9kdWN0
// SIG // cy9NaWNSb29DZXJBdXRfMjAxMC0wNi0yMy5jcmwwWgYI
// SIG // KwYBBQUHAQEETjBMMEoGCCsGAQUFBzAChj5odHRwOi8v
// SIG // d3d3Lm1pY3Jvc29mdC5jb20vcGtpL2NlcnRzL01pY1Jv
// SIG // b0NlckF1dF8yMDEwLTA2LTIzLmNydDANBgkqhkiG9w0B
// SIG // AQsFAAOCAgEAnVV9/Cqt4SwfZwExJFvhnnJL/Klv6lwU
// SIG // tj5OR2R4sQaTlz0xM7U518JxNj/aZGx80HU5bbsPMeTC
// SIG // j/ts0aGUGCLu6WZnOlNN3Zi6th542DYunKmCVgADsAW+
// SIG // iehp4LoJ7nvfam++Kctu2D9IdQHZGN5tggz1bSNU5HhT
// SIG // dSRXud2f8449xvNo32X2pFaq95W2KFUn0CS9QKC/GbYS
// SIG // EhFdPSfgQJY4rPf5KYnDvBewVIVCs/wMnosZiefwC2qB
// SIG // woEZQhlSdYo2wh3DYXMuLGt7bj8sCXgU6ZGyqVvfSaN0
// SIG // DLzskYDSPeZKPmY7T7uG+jIa2Zb0j/aRAfbOxnT99kxy
// SIG // bxCrdTDFNLB62FD+CljdQDzHVG2dY3RILLFORy3BFARx
// SIG // v2T5JL5zbcqOCb2zAVdJVGTZc9d/HltEAY5aGZFrDZ+k
// SIG // KNxnGSgkujhLmm77IVRrakURR6nxt67I6IleT53S0Ex2
// SIG // tVdUCbFpAUR+fKFhbHP+CrvsQWY9af3LwUFJfn6Tvsv4
// SIG // O+S3Fb+0zj6lMVGEvL8CwYKiexcdFYmNcP7ntdAoGokL
// SIG // jzbaukz5m/8K6TT4JDVnK+ANuOaMmdbhIurwJ0I9JZTm
// SIG // dHRbatGePu1+oDEzfbzL6Xu/OHBE0ZDxyKs6ijoIYn/Z
// SIG // cGNTTY3ugm2lBRDBcQZqELQdVTNYs6FwZvKhggLUMIIC
// SIG // PQIBATCCAQChgdikgdUwgdIxCzAJBgNVBAYTAlVTMRMw
// SIG // EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRt
// SIG // b25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRp
// SIG // b24xLTArBgNVBAsTJE1pY3Jvc29mdCBJcmVsYW5kIE9w
// SIG // ZXJhdGlvbnMgTGltaXRlZDEmMCQGA1UECxMdVGhhbGVz
// SIG // IFRTUyBFU046MkFENC00QjkyLUZBMDExJTAjBgNVBAMT
// SIG // HE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2WiIwoB
// SIG // ATAHBgUrDgMCGgMVAGigUorMuMvOqZfF8ttgiWRMRNrz
// SIG // oIGDMIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgT
// SIG // Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAc
// SIG // BgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQG
// SIG // A1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIw
// SIG // MTAwDQYJKoZIhvcNAQEFBQACBQDpLAH2MCIYDzIwMjMx
// SIG // MjE5MTk1MDE0WhgPMjAyMzEyMjAxOTUwMTRaMHQwOgYK
// SIG // KwYBBAGEWQoEATEsMCowCgIFAOksAfYCAQAwBwIBAAIC
// SIG // F5UwBwIBAAICEmwwCgIFAOktU3YCAQAwNgYKKwYBBAGE
// SIG // WQoEAjEoMCYwDAYKKwYBBAGEWQoDAqAKMAgCAQACAweh
// SIG // IKEKMAgCAQACAwGGoDANBgkqhkiG9w0BAQUFAAOBgQAm
// SIG // uyXATE30QsvLPGNpapEZASQinmE24HDBD7MM/rp6dboe
// SIG // mu6AvHEqiqrTORLyTNmlxSmnx5+P372NMubnr4UcnbPW
// SIG // N6ofvhSlRNH0WOfg6Xqn2ezzC+r6STkV+ZeDVb5q7RD/
// SIG // zMPMqwT+jkvDner09IIfOsN5w/2ypuZfibvZDzGCBA0w
// SIG // ggQJAgEBMIGTMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQI
// SIG // EwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
// SIG // HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAk
// SIG // BgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAy
// SIG // MDEwAhMzAAAB3p5InpafKEQ9AAEAAAHeMA0GCWCGSAFl
// SIG // AwQCAQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcN
// SIG // AQkQAQQwLwYJKoZIhvcNAQkEMSIEIJ0i22/SiZxU988L
// SIG // rigC4IAEFjG3gPL9girV0bbe0t73MIH6BgsqhkiG9w0B
// SIG // CRACLzGB6jCB5zCB5DCBvQQgjj4jnw3BXhAQSQJ/5gtz
// SIG // IK0+cP1Ns/NS2A+OB3N+HXswgZgwgYCkfjB8MQswCQYD
// SIG // VQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
// SIG // A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
// SIG // IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQg
// SIG // VGltZS1TdGFtcCBQQ0EgMjAxMAITMwAAAd6eSJ6WnyhE
// SIG // PQABAAAB3jAiBCDLfXkiANe+Y5PIFJp/3mO94bj3XwFf
// SIG // qAri1r7i0nrtQzANBgkqhkiG9w0BAQsFAASCAgAAClR9
// SIG // pIG3elxWw5u9yFKOuRMwJ7jTwSRxhsAKVYs9jIevThlI
// SIG // QLMxgkIA7J4kKHaSgdjVd3oaPPo+zCf8oQ2V5ZVE33+4
// SIG // MSgN9IhEu6LswYupUCmpSUs0JxktNDvNWRHmijMNos6z
// SIG // KMNO9JFxFLRy9yLM0siLDN4eLRsp0lAzgAEwx2WLLosy
// SIG // 3Dq5hzXjUENDMD/J2mOnd42MVfcGlfbjVNQJk/kTIjeY
// SIG // 6eSlP4dQpiJqP/y0upO+RCMiF1SToqEeO6d1kEVXOsvO
// SIG // JiPcLk1iXPoxi1XQfiEMQU4GrweE7GRyxg3dYlakhwgh
// SIG // s70oae36nFrouqDx7Fj4G63GjZSqOp1S0wKn5r0XWcDu
// SIG // 91tNY+i8/Kzez6G+DTsNwBK4lwlRsCT/AM80eR9ddzJs
// SIG // qhFNzRTETuz+ieyRuFzorLhh3W/bFAN9zM8yZadqkUCv
// SIG // 2jNmEoxyob/cYZsYsGaxuAOqKunajP/iwq9XpTv/dXd8
// SIG // RZQp7E0/lueEFDPI4l8Zs8jOhuI44cB4+KG5eP9zKYmj
// SIG // 5PsSowZqhdS7yOPnIqQAP7n9kntcUYON8dEWCQ4tN29k
// SIG // rB8QdGHz2aUMTkzJnIMP/9WiHh4he+jml3omGzdT6g88
// SIG // PKQDgEIpKu/lpU6OOnFnpc1Tms3XhXscV4Ab+53Zyjdr
// SIG // Um6WY0tsTB1jMpshkA==
// SIG // End signature block
