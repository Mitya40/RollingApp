const PI = 3.14159;
let ctx = null, canvas = null;
let center = { x: 350, y: 275 };
let Mas = { mx: 1.0, my: 1.0 };
let drawScale = 1.0;
let pData = {};
let isKantov = 0;
let activeRow = null;

function TNPoint(x, y) { return { x: x, y: y }; }
function TFPoint(x, y, r = 0) { return { x: x, y: y, r: r }; }

function getCanvasPixelData() {
    return ctx.getImageData(0, 0, 700, 550).data;
}
function isLinePixel(x, y, imageData) {
    if (x < 0 || x >= 700 || y < 0 || y >= 550) return false;
    const idx = (y * 700 + x) * 4;
    const r = imageData[idx];
    const g = imageData[idx + 1];
    const b = imageData[idx + 2];

    return !(r >= 245 && g >= 245 && b >= 245);
}

function openDrawingModal(btn) {
    activeRow = btn.closest('tr');
    pData.KOD0 = parseInt(activeRow.querySelector('select[name="KOD0"]').value) || 0;
    pData.KOD1 = parseInt(activeRow.querySelector('select[name="KOD1"]').value) || 0;
    if (pData.KOD0 === 0 || pData.KOD1 === 0) {
        alert("Пожалуйста, выберите тип Подката и Калибра!"); return;
    }
    pData.N = parseInt(activeRow.querySelector('input[name="N"]').value) || 1;
    document.getElementById('drawModalPassN').innerText = pData.N;

    const fields = ["H0", "B0", "H1", "B1", "W", "S", "BVR", "BD", "R", "ROV", "R8"];
    fields.forEach(f => {
        let val = parseFloat(activeRow.querySelector(`input[name="${f}"]`).value.replace(',', '.')) || 0;
        document.getElementById(`m_${f}`).value = val;
        pData[f] = val;
    });

    document.getElementById('m_ROV').readOnly = (pData.KOD1 !== 4 && pData.KOD1 !== 9);
    document.getElementById('m_R8').readOnly = (pData.KOD1 !== 8 && pData.KOD1 !== 5 && pData.KOD1 !== 6);

    canvas = document.getElementById('profileCanvas');
    ctx = canvas.getContext('2d', { willReadFrequently: true });

    const dpr = window.devicePixelRatio || 1;
    canvas.style.width = '700px';
    canvas.style.height = '550px';
    canvas.width = 700 * dpr;
    canvas.height = 550 * dpr;

    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.scale(dpr, dpr);
    center = { x: 350, y: 275 };

    fields.forEach(f => {
        if (f !== 'W') {
            document.getElementById(`m_${f}`).addEventListener('input', () => {
                pData[f] = parseFloat(document.getElementById(`m_${f}`).value) || 0;
                calcAutoParams(); drawProfile();
            });
        }
    });

    canvas.onwheel = function (e) {
        e.preventDefault();
        drawScale += e.deltaY < 0 ? 0.05 : -0.05;
        if (drawScale < 0.1) drawScale = 0.1;
        if (drawScale > 20) drawScale = 20;
        document.getElementById('scaleVal').innerText = drawScale.toFixed(2);
        drawProfile();
    };

    determineKantovka();
    calcAutoParams();
    calculateScale();
    drawProfile();

    new bootstrap.Modal(document.getElementById('drawingModal')).show();
}

function confirmDrawing() {
    const fields = ["H0", "B0", "H1", "B1", "W", "S", "BVR", "BD", "R", "ROV", "R8"];
    fields.forEach(f => {
        activeRow.querySelector(`input[name="${f}"]`).value = parseFloat(pData[f].toFixed(3));
    });
    bootstrap.Modal.getInstance(document.getElementById('drawingModal')).hide();
}

function calcAutoParams() {
    if (pData.KOD1 === 4 && pData.H1 > pData.S) {
        pData.ROV = (pData.H1 - pData.S) * (1 + Math.pow(pData.BVR / (pData.H1 - pData.S), 2)) / 4;
        document.getElementById('m_ROV').value = pData.ROV.toFixed(3);
    }
    let W = 0, A1 = pData.H1 > 0 ? pData.B1 / pData.H1 : 0, DEL1 = 1, BK = 1, AK = 1;
    switch (pData.KOD1) {
        case 1: case 0: BK = pData.B1; W = A1 * pData.H1 * pData.H1;
            if (pData.KOD0 === 2) W *= (1 - 0.333 * (1 - Math.sqrt(Math.max(0, 1 - 1 / (A1 * A1))))); break;
        case 2: BK = pData.H1 / Math.cos(15.5 / 57.3); DEL1 = pData.B1 / BK;
            W = (0.785 - 0.667 * (1 - DEL1) * Math.sqrt(Math.max(0, 1 - DEL1 * DEL1))) * pData.H1 * pData.H1; break;
        case 3: BK = (pData.H1 + 0.83 * pData.R); DEL1 = pData.B1 / BK;
            let cc = BK / 1.414; W = (DEL1 * (2 - DEL1) - 0.43 * Math.pow(pData.R / cc, 2)) * cc * cc; break;
        case 4: BK = pData.H1 * Math.sqrt(Math.max(0, (4 * pData.ROV / pData.H1) - 1)); DEL1 = pData.B1 / BK;
            W = 0.6 * (2.07 - DEL1) * (A1 + 0.66 * DEL1 - 0.43) * pData.H1 * pData.H1; break;
        case 5: BK = pData.BD + pData.H1; DEL1 = pData.B1 / BK; AK = BK / pData.H1;
            W = ((AK - 0.215) - 0.667 * (1 - DEL1) * Math.sqrt(Math.max(0, 1 - DEL1 * DEL1))) * pData.H1 * pData.H1; break;
        case 6: case 8: if (pData.H1 > pData.S) {
            BK = pData.BD + pData.H1 * (pData.BVR - pData.BD) / (pData.H1 - pData.S); DEL1 = pData.B1 / BK;
            if (pData.KOD0 === 6) W = 0.83 * (pData.H1 / 2) * (pData.H1 / 2);
            else W = pData.H1 * (pData.BD + (1 + (1 - DEL1) / (1 - pData.BD / BK)) * (pData.B1 - pData.BD) / 2) - 0.088 * pData.R * pData.R;
        } break;
        case 7: BK = pData.BVR + (pData.BVR / (pData.H1 + 2 * pData.R - pData.S)) * pData.S; DEL1 = pData.B1 / BK; AK = BK / pData.H1;
            W = (0.5 * AK * DEL1 * (2 - DEL1) - 0.43 * Math.pow(pData.R / pData.H1, 2)) * pData.H1 * pData.H1; break;
        case 9: BK = pData.BVR + 2 * pData.ROV * (1 - Math.cos(Math.asin(pData.S / (2 * pData.ROV)))); DEL1 = pData.B1 / BK; AK = BK / pData.H1;
            let c9 = 1 / (AK * AK) - 1, a9 = 1 + 1 / (AK * AK), b9 = 1 + 1 / AK;
            W = 0.15 * AK * AK * (a9 * a9 * (2.07 - DEL1) * (1.66 * DEL1 - 0.43) - 0.833 * c9 * b9 * b9) * pData.H1 * pData.H1; break;
        case 10: W = 0.866 * Math.pow(pData.H1 / 1.154, 2); break;
    }
    if (isNaN(W) || W < 0 || !isFinite(W)) W = 0;
    pData.W = W; document.getElementById('m_W').value = W.toFixed(3);
}

function fillet(A, B, C) {
    let n1 = A.x - B.x, n2 = A.y - B.y;
    let BA = Math.sqrt(n1 * n1 + n2 * n2);
    if (BA === 0) return { B1: TFPoint(B.x, B.y), E1: TFPoint(B.x, B.y), Ctr: TFPoint(B.x, B.y, 0) };
    n1 /= BA; n2 /= BA;
    let m1 = C.x - B.x, m2 = C.y - B.y;
    let BC = Math.sqrt(m1 * m1 + m2 * m2);
    if (BC === 0) return { B1: TFPoint(B.x, B.y), E1: TFPoint(B.x, B.y), Ctr: TFPoint(B.x, B.y, 0) };
    m1 /= BC; m2 /= BC;
    let s1 = n1 + m1, s2 = n2 + m2;
    let lenS = Math.sqrt(s1 * s1 + s2 * s2);
    let b1 = lenS > 0.001 ? s1 / lenS : 0, b2 = lenS > 0.001 ? s2 / lenS : 0;
    let cosa = lenS / 2;
    let sina = Math.sqrt(Math.max(0, 1 - cosa * cosa));
    let q = sina > 0.0001 ? B.r / sina : B.r * 1.42;
    let BO1 = q * b1, BO2 = q * b2;
    let Ctr = TFPoint(B.x + BO1, B.y + BO2, Math.sqrt(B.r * b1 * B.r * b1 + B.r * b2 * B.r * b2));
    let proj = BO1 * n1 + BO2 * n2;
    return { B1: TFPoint(B.x + proj * n1, B.y + proj * n2), E1: TFPoint(B.x + proj * m1, B.y + proj * m2), Ctr: Ctr };
}

function angle(x2, y2, x1, y1) {
    let x = x2 - x1, y = y2 - y1;
    if (Math.abs(x) < 0.0001 && Math.abs(y) < 0.0001) return 0;
    if (Math.abs(y) < 0.0001 * Math.abs(x)) return x > 0 ? 0 : PI;
    if (x > 0) return y > 0 ? Math.atan(Math.abs(y / x)) : -Math.atan(Math.abs(y / x));
    if (y > 0) return Math.abs(x) < 0.0001 * Math.abs(y) ? PI / 2 : -Math.atan(Math.abs(y / x)) + PI;
    return Math.abs(x) < 0.0001 * Math.abs(y) ? PI * 3 / 2 : Math.atan(Math.abs(y / x)) + PI;
}

function compare(angObj) {
    if (Math.abs(angObj.x - angObj.y) > 3.14 - 0.0001) {
        if (angObj.x < 0) angObj.x += 6.28;
        else if (angObj.y < 0) angObj.y += 6.28;
        else if (angObj.x > angObj.y) angObj.y += 6.28;
        else angObj.x += 6.28;
    }
}

function newPoint(r, b, First, Second, Third, Type1) {
    let yback = 0;
    switch (Type1) {
        case 8: case 7: case 3:
            if (Math.abs(b) < Math.abs(Third.x)) yback = Third.y;
            else if (Math.abs(b) < Math.abs(Second.x)) yback = Math.sqrt(Math.max(0, r * r - Math.pow(b - Third.x, 2))) + Third.y - r;
            else if (Math.abs(b) < Math.abs(First.x)) yback = (Math.abs(b) - Math.abs(Second.x)) * (Math.abs(First.y) - Math.abs(Second.y)) / (Math.abs(First.x) - Math.abs(Second.x)) + Second.y;
            else yback = First.y; break;
        case 6:
            if (Math.abs(b) < Math.abs(Second.x)) yback = (b - Second.x) * (Third.y - Second.y) / (Third.x - Second.x) + Second.y;
            else if (Math.abs(b) < Math.abs(First.x)) yback = (b - First.x) * (Second.y - First.y) / (Second.x - First.x) + First.y;
            else yback = First.y; break;
        case 2:
            if (Math.abs(b) < Math.abs(Third.x)) yback = Math.sqrt(Math.max(0, r * r - b * b));
            else if (Math.abs(b) < Math.abs(Second.x)) yback = (b - Third.x) * (Second.y - Third.y) / (Second.x - Third.x) + Third.y;
            else yback = Second.y; break;
        case 4:
            yback = 0;
            if (Math.abs(b) < Math.abs(Second.x)) yback = Math.sqrt(Math.max(0, r * r - b * b - r * r + Second.x * Second.x));
            if (Math.abs(b) > Math.abs(Second.x) - 0.00001) yback = Second.y;
            break;
        case 5:
            if (Math.abs(b) < Math.abs(Third.x)) yback = Third.y;
            else if (Math.abs(b) < Math.abs(Second.x)) yback = Math.sqrt(Math.max(0, r * r - Math.pow(b - Third.x, 2)));
            else yback = Second.y; break;
        case 9:
            if (Math.abs(b) > Math.abs(Second.x)) yback = Second.y;
            else if (Math.abs(b) < Math.abs(Second.x) - 0.00001)
                yback = r * r - Math.pow(b + Math.sqrt(Math.max(0, r * r - Third.y * Third.y)), 2);
            else yback = 0;
            break;
        case 1: yback = Third.y; break;
        case 10:
            if (Math.abs(b) < Math.abs(Third.x)) yback = Third.y;
            else if (Math.abs(b) < Math.abs(Second.x)) yback = (b - Third.x) * (Second.y - Third.y) / (Second.x - Third.x) + Third.y;
            else if (Math.abs(b) < Math.abs(Second.x) + 0.1 && Math.abs(b) > Math.abs(Second.x) - 1) yback = Second.y;
            else yback = First.y; break;
    }
    return yback;
}

// --- УТИЛИТЫ ОТРИСОВКИ ---
function calculateScale() {
    const pixelsPerMm = 96 / 25.4;
    Mas.mx = pixelsPerMm;
    Mas.my = pixelsPerMm;
    let MaxY = Math.max(pData.H1, pData.H0);
    let MaxX = Math.max(pData.BVR + pData.H1 / 10, pData.B0);
    if (MaxY < 1) MaxY = 1;
    if (MaxX < 1) MaxX = 1;

    let fitScaleX = (350 - 50) / (MaxX * pixelsPerMm);
    let fitScaleY = (275 - 50) / (MaxY * pixelsPerMm);

    drawScale = Math.min(fitScaleX, fitScaleY);
    document.getElementById('scaleVal').innerText = drawScale.toFixed(2);
}

function setRealPhysicalScale() {
    drawScale = 1.0;
    document.getElementById('scaleVal').innerText = drawScale.toFixed(2);
    drawProfile();
}

function GetX(x) { return center.x + Mas.mx * drawScale * x; }
function GetY(y) { return center.y - Mas.my * drawScale * y; }

function SetLineMode(mode) {
    ctx.beginPath();
    if (mode === 'Solid2') { ctx.strokeStyle = "blue"; ctx.lineWidth = 2; ctx.setLineDash([]); }
    if (mode === 'SoliDash') { ctx.strokeStyle = "red"; ctx.lineWidth = 1; ctx.setLineDash([5, 5]); }
    if (mode === 'Solid') { ctx.strokeStyle = "black"; ctx.lineWidth = 1; ctx.setLineDash([]); }
}

function MoveTo(x, y, kant = 0) {
    if ((kant & 8) === 8) ctx.moveTo(GetX(y), GetY(x));
    else if ((kant & 16) === 16) ctx.moveTo(GetX(x * 0.70710678 + y * 0.70710678), GetY(-x * 0.70710678 + y * 0.70710678));
    else ctx.moveTo(GetX(x), GetY(y));
}

function LineTo(x, y, kant = 0) {
    if ((kant & 8) === 8) ctx.lineTo(GetX(y), GetY(x));
    else if ((kant & 16) === 16) ctx.lineTo(GetX(x * 0.70710678 + y * 0.70710678), GetY(-x * 0.70710678 + y * 0.70710678));
    else ctx.lineTo(GetX(x), GetY(y));
}

function MoveToN(p) { ctx.moveTo(p.x, p.y); }
function LineToN(p) { ctx.lineTo(p.x, p.y); }
function LineToNX(x, y) { ctx.lineTo(x, y); }
function TextOut(x, y, text) {
    ctx.font = "bold 13px Arial"; ctx.fillStyle = "black";
    ctx.fillText(text, x, y); ctx.beginPath();
}

function arcs(B1, E1, Ctr, fp, Nparris) {
    if (Math.abs(B1.x - E1.x) < 0.0001 && Math.abs(B1.y - E1.y) < 0.0001) return B1;

    // ЗАЩИТА ОТ БЕСКОНЕЧНЫХ РАДИУСОВ:
    if (Ctr.r > 2000) {
        if (fp && fp.x > -9999 && fp.y > -9999) {
            MoveTo(fp.x, fp.y, Nparris); LineTo(B1.x, B1.y, Nparris);
        } else {
            MoveTo(B1.x, B1.y, Nparris);
        }
        LineTo(E1.x, E1.y, Nparris);
        return E1;
    }

    let angObj = { x: angle(B1.x, B1.y, Ctr.x, Ctr.y), y: angle(E1.x, E1.y, Ctr.x, Ctr.y) };
    compare(angObj);
    let startAng = angObj.x, endAng = angObj.y, i = startAng;
    let t1 = Ctr.x + Ctr.r * Math.cos(startAng), t2 = Ctr.y + Ctr.r * Math.sin(startAng);

    if (fp && fp.x > -9999 && fp.y > -9999) {
        MoveTo(fp.x, fp.y, Nparris); LineTo(B1.x, B1.y, Nparris); LineTo(t1, t2, Nparris);
    } else {
        MoveTo(B1.x, B1.y, Nparris); LineTo(t1, t2, Nparris);
    }

    let lastPt = { x: Ctr.x + Ctr.r * Math.cos(i), y: Ctr.y + Ctr.r * Math.sin(i) };
    while (((i < endAng) && (endAng > startAng)) || ((i > endAng) && (endAng < startAng))) {
        let ci = Math.cos(i), si = Math.sin(i);
        let x10 = Ctr.x + Ctr.r * ci, y10 = Ctr.y + Ctr.r * si;
        LineTo(x10, y10, Nparris);
        lastPt = { x: x10, y: y10 };
        if (endAng > startAng) i += PI / 180; else i -= PI / 180;
    }
    if (((i < endAng + PI / 180) && (endAng + PI / 180 > startAng)) || ((i + PI / 180 > endAng) && (endAng < startAng + PI / 180))) {
        let c_end = Math.cos(endAng), s_end = Math.sin(endAng);
        let x_end = Ctr.x + Ctr.r * c_end, y_end = Ctr.y + Ctr.r * s_end;
        LineTo(x_end, y_end, Nparris);
        lastPt = { x: x_end, y: y_end };
    }
    return TFPoint(lastPt.x, lastPt.y);
}

function getDrawWidthForPodkat(mB1) {
    if ((isKantov & 8) === 8) return pData.H0 / 2;
    if ((isKantov & 16) === 16) return mB1 / 2;
    return pData.B0 / 2;
}

function PicSimpleLine(B1) {
    const x0 = GetX(-B1 / 2);
    const y0 = GetY(0);

    // ЗАЩИТА ОТ БЕСКОНЕЧНОСТИ: Не ищем пиксели за пределами холста
    if (x0 < 0 || x0 >= 700) return; 

    let imageData = getCanvasPixelData();
    // ищем верхнюю границу (по пикселям, пока не встретим линию)
    let i = y0;
    while (i > 0 && !isLinePixel(x0, i, imageData)) i--;
    let j = y0;
    while (j < 550 && !isLinePixel(x0, j, imageData)) j++;

    ctx.save();
    ctx.strokeStyle = "red";
    ctx.lineWidth = 1;
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.moveTo(x0, i);
    ctx.lineTo(x0, j);
    ctx.stroke();

    const x1 = GetX(B1 / 2);
    ctx.beginPath();
    ctx.moveTo(x1, i);
    ctx.lineTo(x1, j);
    ctx.stroke();
    ctx.restore();
}

function edgeBand(kant, Kalibr_, B1, H1_, BVR_, H1) {
    // Для большинства калибров используется PicSimpleLine
    switch (Kalibr_) {
        case 1: case 4: case 5: case 6: case 8: case 9: case 10: case 11: case 12:
            PicSimpleLine(B1);
            break;
        case 3: case 7:
            if (!(kant & 16)) {
                let tgALPHA = 1;
                let x0 = GetX(-B1 / 2), y0 = GetY(0);
                let xEnd = GetX(0);

                // ЗАЩИТА: Если линия за пределами холста, прерываемся
                if (x0 < 0 || x0 >= 700) break;

                let i = 0, y1 = y0;
                // ИСПРАВЛЕНИЕ: Читаем пиксели ОДИН раз до цикла
                let imageData = getCanvasPixelData();

                while (i < (700 - x0) && y1 > 0) {
                    // Передаем уже считанный массив imageData
                    if (isLinePixel(x0 + i, y1, imageData) && (x0 + i != xEnd)) {
                        y1 = y0 - tgALPHA * i;
                        i++;
                    } else break;
                }
                // рисуем линии
                ctx.save(); ctx.strokeStyle = "red"; ctx.lineWidth = 1;
                ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x0 + i - 1, y1); ctx.stroke();
                ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x0 + i - 1, 2 * y0 - y1); ctx.stroke();
                let x2 = GetX(B1 / 2);
                ctx.beginPath(); ctx.moveTo(x2, y0); ctx.lineTo(x2 - i + 1, y1); ctx.stroke();
                ctx.beginPath(); ctx.moveTo(x2, y0); ctx.lineTo(x2 - i + 1, 2 * y0 - y1); ctx.stroke();
                ctx.restore();
            } else {
                PicSimpleLine(B1);
            }
            break;
        case 2:
            if (!(H1 > H1_) && Math.abs(GetY(H1_ / 2) - GetY(0)) > 8) {
                let r = Math.abs(GetY(H1 / 2) - GetY(0));
                let x0 = GetX(-B1 / 2) + r;
                let y0 = GetY(0);

            } else {
                PicSimpleLine(B1);
            }
            break;
        default:
            PicSimpleLine(B1);
    }
}

function strela(DownLeftArr, DownWriteArr, UpLeftArr, UpWriteArr, Placech1, ch1, Placech2, ch2, UpDown, LeftWrite) {
    if (!LeftWrite && UpDown) {
        MoveToN(DownLeftArr); LineToNX(UpLeftArr.x, UpLeftArr.y - 4 * UpDown);
        MoveToN(DownWriteArr); LineToNX(UpWriteArr.x, UpWriteArr.y - 4 * UpDown);
        MoveToN(TNPoint(UpLeftArr.x + 5, UpLeftArr.y - 3 * UpDown)); LineToN(UpLeftArr); LineToN(UpWriteArr);
        LineToNX(UpWriteArr.x - 5, UpWriteArr.y + 3 * UpDown);
        MoveToN(TNPoint(UpLeftArr.x + 5, UpLeftArr.y + 3 * UpDown)); LineToN(UpLeftArr);
        MoveToN(TNPoint(UpWriteArr.x - 5, UpWriteArr.y - 3 * UpDown)); LineToN(UpWriteArr);
        ctx.stroke(); TextOut(Placech1.x, Placech1.y, ch1); TextOut(Placech2.x, Placech2.y, ch2);
    } else if (LeftWrite && !UpDown) {
        MoveToN(DownWriteArr); LineToNX(DownLeftArr.x - 4 * LeftWrite, DownLeftArr.y);
        MoveToN(UpWriteArr); LineToNX(UpLeftArr.x - 4 * LeftWrite, UpLeftArr.y);
        MoveToN(TNPoint(UpLeftArr.x - 3 * LeftWrite, UpLeftArr.y + 5)); LineToN(UpLeftArr); LineToN(DownLeftArr);
        LineToNX(DownLeftArr.x + 3 * LeftWrite, DownLeftArr.y - 5);
        MoveToN(TNPoint(UpLeftArr.x + 3 * LeftWrite, UpLeftArr.y + 5)); LineToN(UpLeftArr);
        MoveToN(TNPoint(DownLeftArr.x - 3 * LeftWrite, DownLeftArr.y - 5)); LineToN(DownLeftArr);
        ctx.stroke(); TextOut(Placech1.x, Placech1.y, ch1); TextOut(Placech2.x, Placech2.y, ch2);
    }
}

function radius(A, O, Placer1, r1, Placer2, r1i) {
    let coner = -angle(A.x, A.y, O.x, O.y);
    let dx = 0, dx1 = 0, dx2 = 0, dy = 0, dy1 = 0, dy2 = 0;
    if (coner > PI * 2) coner -= PI * 2; if (coner < -PI / 8) coner += PI * 2;
    if (coner > PI * (3 / 2 + 3 / 8 + 0.01)) coner -= PI * 2;
    if (coner > -PI / 8 - 0.02 && coner < PI / 8 - 0.01) { dx = -5; dy1 = 3; dy2 = -3; }
    else if (coner > PI / 8 - 0.02 && coner < PI * 3 / 8 + 0.01) { dx1 = -5; dy2 = 5; }
    else if (coner > PI * 3 / 8 - 0.02 && coner < PI * 5 / 8 - 0.01) { dy = 5; dx1 = 3; dx2 = -3; }
    else if (coner > PI * 5 / 8 - 0.02 && coner < PI * 7 / 8 + 0.01) { dx1 = 5; dy2 = 5; dx2 = 1; }
    else if (coner > PI * 7 / 8 - 0.02 && coner < PI * 9 / 8 - 0.01) { dx = 5; dy1 = 3; dy2 = -3; }
    else if (coner > PI * 9 / 8 - 0.02 && coner < PI * 11 / 8 - 0.01) { dx1 = 5; dy2 = -5; }
    else if (coner > PI * 11 / 8 - 0.02 && coner < PI * 13 / 8 - 0.01) { dy = -5; dx1 = 3; dx2 = -3; }
    else if (coner > PI * 13 / 8 - 0.02 && coner < PI * 15 / 8 + 0.01) { dx1 = -5; dy2 = -5; }
    MoveToN(TNPoint(A.x + 1 + dx + dx1, A.y + 1 + dy + dy1)); LineToN(TNPoint(A.x + 1, A.y + 1));
    LineToN(TNPoint(A.x + 1 + dx + dx2, A.y + 1 + dy + dy2)); MoveToN(TNPoint(A.x + 1, A.y + 1)); LineToN(TNPoint(O.x + 1, O.y + 1));
    ctx.stroke(); TextOut(Placer1.x, Placer1.y, r1); TextOut(Placer2.x, Placer2.y, r1i);
}

// --- ФУНКЦИИ ОТРИСОВКИ ФИГУР ---
function bochka(H1, B1, rd, Nparris) {
    if ((Nparris & 1) === 1) {
        SetLineMode('Solid2');
        MoveTo(-B1 / 2 - H1 / 4, H1 / 2, Nparris); LineTo(B1 / 2 + H1 / 4, H1 / 2, Nparris);
        MoveTo(-B1 / 2 - H1 / 4, -H1 / 2, Nparris); LineTo(B1 / 2 + H1 / 4, -H1 / 2, Nparris); ctx.stroke();
    }
    if ((Nparris & 4) === 4) {
        if (!((Nparris & 32) === 32)) {
            SetLineMode('SoliDash');
            let r = 0.15 * H1; if (r > B1 / 2 - 1) r = 0;
            MoveTo(-B1 / 2, H1 / 2 - r, Nparris); LineTo(-B1 / 2, r - H1 / 2, Nparris);
            arcs(TFPoint(-B1 / 2, -r + H1 / 2), TFPoint(-B1 / 2 + r, H1 / 2), TFPoint(-B1 / 2 + r, -r + H1 / 2, r), TFPoint(-10000, -10000), Nparris);
            MoveTo(-B1 / 2 + r, H1 / 2, Nparris); LineTo(B1 / 2 - r, H1 / 2, Nparris);
            arcs(TFPoint(B1 / 2 - r, H1 / 2), TFPoint(B1 / 2, H1 / 2 - r), TFPoint(B1 / 2 - r, -r + H1 / 2, r), TFPoint(-10000, -10000), Nparris);
            MoveTo(B1 / 2, H1 / 2 - r, Nparris); LineTo(B1 / 2, -H1 / 2 + r, Nparris);
            arcs(TFPoint(B1 / 2, r - H1 / 2), TFPoint(B1 / 2 - r, -H1 / 2), TFPoint(B1 / 2 - r, r - H1 / 2, r), TFPoint(-10000, -10000), Nparris);
            MoveTo(B1 / 2 - r, -H1 / 2, Nparris); LineTo(-B1 / 2 + r, -H1 / 2, Nparris);
            arcs(TFPoint(-B1 / 2 + r, -H1 / 2), TFPoint(-B1 / 2, -H1 / 2 + r), TFPoint(r - B1 / 2, -H1 / 2 + r, r), TFPoint(-10000, -10000), Nparris);
            ctx.stroke();
        } else if (rd > H1 / 2) {
            SetLineMode('SoliDash');
            let xo = B1 / 2 - 0.15 * H1 - Math.sqrt(rd * rd - (H1 / 2) * (H1 / 2));
            MoveTo(-B1 / 2 + 0.15 * H1, H1 / 2, Nparris); LineTo(B1 / 2 - 0.15 * H1, H1 / 2, Nparris);
            arcs(TFPoint(B1 / 2 - 0.15 * H1, H1 / 2), TFPoint(B1 / 2 - 0.15 * H1, -H1 / 2), TFPoint(xo, 0, rd), TFPoint(-10000, -10000), Nparris);
            MoveTo(B1 / 2 - 0.15 * H1, -H1 / 2, Nparris); LineTo(-B1 / 2 + 0.15 * H1, -H1 / 2, Nparris);
            arcs(TFPoint(-B1 / 2 + 0.15 * H1, -H1 / 2), TFPoint(-B1 / 2 + 0.15 * H1, H1 / 2), TFPoint(-xo, 0, rd), TFPoint(-10000, -10000), Nparris);
            ctx.stroke();
        }
    }
    if ((Nparris & 2) === 2) {
        SetLineMode('SoliDash'); MoveTo(-B1 / 2 - H1 / 4 - 5, 0, Nparris); LineTo(B1 / 2 + H1 / 4 + 5, 0, Nparris);
        MoveTo(0, -H1 / 2 + 10, Nparris); LineTo(0, H1 / 2 - 10, Nparris); ctx.stroke();
        SetLineMode('Solid');
        strela(TNPoint(GetX(-B1 / 2 - H1 / 4) - 10, GetY(-H1 / 2)), TNPoint(GetX(0), GetY(-H1 / 2)),
            TNPoint(GetX(-B1 / 2 - H1 / 4) - 10, GetY(H1 / 2)), TNPoint(GetX(0), GetY(H1 / 2)),
            TNPoint(GetX(-B1 / 2 - H1 / 4) - 32, GetY(0)), "H", TNPoint(GetX(-B1 / 2 - H1 / 4) - 21, GetY(0) + 5), "1", 0, 1);
    }
}

function krug(H1, BVR, S, b, Nparris) {
    if (Math.abs(BVR) < 0.0001 && Math.abs(S) < 0.0001 && Math.abs(b) < 0.0001 && (Nparris & 4) === 4) {
        SetLineMode('SoliDash');
        let t1 = arcs(TFPoint(0, H1 / 2), TFPoint(H1 / 2, 0), TFPoint(0, 0, H1 / 2), TFPoint(-10000, -10000), Nparris);
        MoveTo(t1.x, t1.y, 0); LineTo(H1 / 2, 0, 0);
        let t2 = arcs(TFPoint(H1 / 2, 0), TFPoint(0, -H1 / 2), TFPoint(0, 0, H1 / 2), TFPoint(-10000, -10000), Nparris);
        MoveTo(t2.x, t2.y, 0); LineTo(0, -H1 / 2, 0);
        let t3 = arcs(TFPoint(0, -H1 / 2), TFPoint(-H1 / 2, 0), TFPoint(0, 0, H1 / 2), TFPoint(-10000, -10000), Nparris);
        MoveTo(t3.x, t3.y, 0); LineTo(-H1 / 2, 0, 0);
        let t4 = arcs(TFPoint(-H1 / 2, 0), TFPoint(0, H1 / 2), TFPoint(0, 0, H1 / 2), TFPoint(-10000, -10000), Nparris);
        MoveTo(t4.x, t4.y, 0); LineTo(0, H1 / 2, 0); ctx.stroke();
        return;
    }
    let r_ = H1 / 2, alpha = 27.0 / 180.0 * PI;
    if (H1 > 105 && H1 < 565) alpha = 12.0 / 180.0 * PI; else if (H1 > 50 && H1 < 55) alpha = 15.0 / 180.0 * PI;
    else if (H1 > 30 && H1 < 45) alpha = 22.0 / 180.0 * PI; else if (H1 > 10 && H1 < 30) alpha = 27.0 / 180.0 * PI;

    for (let i of [1, -1]) {
        for (let i1 of [1, -1]) {
            if ((Nparris & 1) === 1) {
                SetLineMode('Solid2'); MoveTo((-H1 / 4 - BVR / 2) * i1, S / 2 * i, Nparris);
                let res = fillet(TFPoint((-H1 / 4 - BVR / 2) * i1, S / 2 * i), TFPoint(-BVR / 2 * i1, S / 2 * i, H1 / 10), TFPoint(-r_ * Math.cos(alpha) * i1, r_ * Math.sin(alpha) * i));
                LineTo(res.B1.x, res.B1.y, Nparris); arcs(res.B1, res.E1, res.Ctr, TFPoint(-10000, -10000), Nparris);
                LineTo(-r_ * Math.cos(alpha) * i1, r_ * Math.sin(alpha) * i, Nparris);
                arcs(TFPoint(0, r_ * i), TFPoint(-r_ * Math.cos(alpha) * i1, r_ * Math.sin(alpha) * i), TFPoint(0, 0, r_), TFPoint(-10000, -10000), Nparris);
                ctx.stroke();
            }
            if ((Nparris & 4) === 4) {
                SetLineMode('SoliDash');
                let y = newPoint(H1 / 2, b, TFPoint(0, 0), TFPoint(BVR / 2, S / 2, H1 / 10), TFPoint(r_ * Math.cos(alpha), r_ * Math.sin(alpha)), 2);
                MoveTo(0, r_ * i, Nparris);
                if (Math.abs(b) < Math.abs(r_ * Math.cos(alpha))) arcs(TFPoint(0, r_ * i), TFPoint(-b * i1, y * i), TFPoint(0, 0, r_), TFPoint(-10000, -10000), Nparris);
                else {
                    arcs(TFPoint(0, r_ * i), TFPoint(-r_ * Math.cos(alpha) * i1, r_ * Math.sin(alpha) * i), TFPoint(0, 0, r_), TFPoint(-10000, -10000), Nparris);
                    if (Math.abs(b) > Math.abs(r_ * Math.cos(alpha)) && Math.abs(b) < Math.abs(BVR / 2)) LineTo(-b * i1, y * i, Nparris);
                    else if (Math.abs(b) > Math.abs(BVR / 2)) { LineTo(-BVR / 2 * i1, S / 2 * i, Nparris); LineTo(-b * i1, y * i, Nparris); }
                }
                LineTo(-b * i1, y * i, Nparris); LineTo(-b * i1, 0, Nparris); ctx.stroke();
            }
        }
    }
    if ((Nparris & 2) === 2) {
        SetLineMode('SoliDash'); MoveTo(-H1 / 4 - BVR / 2 - 10, 0, Nparris); LineTo(H1 / 4 + BVR / 2 + 10, 0, Nparris);
        MoveTo(0, -H1 / 2 + 10, Nparris); LineTo(0, H1 / 2 - 10, Nparris); ctx.stroke();
        SetLineMode('Solid');
        strela(TNPoint(GetX(-BVR / 2), GetY(S / 2 + 0.16 * H1 / 10)), TNPoint(GetX(BVR / 2), GetY(S / 2 + 0.16 * H1 / 10)),
            TNPoint(GetX(-BVR / 2), GetY(H1 / 2) - 30), TNPoint(GetX(BVR / 2), GetY(H1 / 2) - 30),
            TNPoint(GetX(0) - 7, GetY(H1 / 2) - 50), "B", TNPoint(GetX(0) + 2, GetY(H1 / 2) - 47), "вр", 1, 0);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(-H1 / 2)), TNPoint(GetX(0), GetY(-H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(H1 / 2)), TNPoint(GetX(0), GetY(H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 32, GetY(0)), "H", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "1", 0, 1);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 2, GetY(S / 2) - 20), "S", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "", 0, 1);
    }
}

function oval(H1, BVR, BD, S, r, b, Nparris) {
    let cosa = (H1 / 10 + r - H1 / 2 + S / 2) / (r + H1 / 10), sina = Math.sqrt(1 - cosa * cosa);
    let x1 = (r + H1 / 10) * sina, x2 = r * sina, y2 = r * cosa + H1 / 2 - r;
    for (let i of [1, -1]) {
        if ((Nparris & 1) === 1) {
            SetLineMode('Solid2'); MoveTo(-H1 / 4 - BVR / 2, S / 2 * i, Nparris); LineTo(-x1, S / 2 * i, Nparris);
            arcs(TFPoint(-x1, S / 2 * i), TFPoint(-x2, y2 * i), TFPoint(-x1, (H1 / 10 + S / 2) * i, H1 / 10), TFPoint(-10000, -10000), Nparris);
            if (BD < 0.0001) arcs(TFPoint(-x2, y2 * i), TFPoint(x2, y2 * i), TFPoint(0, (H1 / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
            else {
                let yd = Math.sqrt(r * r - (BD / 2) * (BD / 2)) + H1 / 2 - r;
                arcs(TFPoint(-x2, y2 * i), TFPoint(-BD / 2, yd * i), TFPoint(0, (H1 / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                MoveTo(-BD / 2, yd * i, Nparris); LineTo(BD / 2, yd * i, Nparris);
                arcs(TFPoint(x2, y2 * i), TFPoint(BD / 2, yd * i), TFPoint(0, (H1 / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
            }
            MoveTo(x1, S / 2 * i, Nparris); LineTo(H1 / 4 + BVR / 2, S / 2 * i, Nparris);
            arcs(TFPoint(x1, S / 2 * i), TFPoint(x2, y2 * i), TFPoint(x1, (H1 / 10 + S / 2) * i, H1 / 10), TFPoint(-10000, -10000), Nparris); ctx.stroke();
        }
        if ((Nparris & 4) === 4) {
            SetLineMode('SoliDash');
            let y = newPoint(r, b, TFPoint(0, 0), TFPoint(BVR / 2, S / 2 * i, 0), TFPoint(0, 0, 0), 4);
            if (b < BVR / 2) {
                let t_x1 = b, t_y1 = Math.sqrt(1 - (b / r) * (b / r)) * r - (r - H1 / 2);
                MoveTo(t_x1, t_y1 * i, Nparris); LineTo(t_x1, 0, Nparris);
                MoveTo(-t_x1, t_y1 * i, Nparris); LineTo(-t_x1, 0, Nparris);
                if (BD < 0.0001) arcs(TFPoint(-t_x1, t_y1 * i), TFPoint(t_x1, t_y1 * i), TFPoint(0, (H1 / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                else {
                    let yd = Math.sqrt(r * r - (BD / 2) * (BD / 2)) + H1 / 2 - r;
                    arcs(TFPoint(-t_x1, t_y1 * i), TFPoint(-BD / 2, yd * i), TFPoint(0, (H1 / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                    MoveTo(-BD / 2, yd * i, Nparris); LineTo(BD / 2, yd * i, Nparris);
                    arcs(TFPoint(t_x1, t_y1 * i), TFPoint(BD / 2, yd * i), TFPoint(0, (H1 / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                }
            } else {
                let t_x1 = BVR / 2, t_y1 = S / 2;
                MoveTo(BVR / 2, S / 2 * i, Nparris); LineTo(t_x1, t_y1 * i, Nparris); LineTo(b, t_y1 * i, Nparris); LineTo(b, 0, Nparris);
                MoveTo(-BVR / 2, S / 2 * i, Nparris); LineTo(-t_x1, t_y1 * i, Nparris); LineTo(-b, t_y1 * i, Nparris); LineTo(-b, 0, Nparris);
                if (BD < 0.0001) arcs(TFPoint(-t_x1, t_y1 * i), TFPoint(t_x1, t_y1 * i), TFPoint(0, (H1 / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                else {
                    let yd = Math.sqrt(r * r - (BD / 2) * (BD / 2)) + H1 / 2 - r;
                    arcs(TFPoint(-t_x1, t_y1 * i), TFPoint(-BD / 2, yd * i), TFPoint(0, (H1 / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                    MoveTo(-BD / 2, yd * i, Nparris); LineTo(BD / 2, yd * i, Nparris);
                    arcs(TFPoint(t_x1, t_y1 * i), TFPoint(BD / 2, yd * i), TFPoint(0, (H1 / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                }
            }
            ctx.stroke();
        }
    }
    if ((Nparris & 2) === 2) {
        SetLineMode('SoliDash'); MoveTo(-H1 / 4 - BVR / 2 - 10, 0, Nparris); LineTo(H1 / 4 + BVR / 2 + 10, 0, Nparris);
        MoveTo(0, -H1 / 2 + 10, Nparris); LineTo(0, H1 / 2 - 10, Nparris); ctx.stroke();
        SetLineMode('Solid');
        strela(TNPoint(GetX(-BVR / 2), GetY(S / 2)), TNPoint(GetX(BVR / 2), GetY(S / 2)),
            TNPoint(GetX(-BVR / 2), GetY(H1 / 2) - 30), TNPoint(GetX(BVR / 2), GetY(H1 / 2) - 30),
            TNPoint(GetX(0) - 7, GetY(H1 / 2) - 50), "B", TNPoint(GetX(0) + 2, GetY(H1 / 2) - 48), "вр", 1, 0);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(-H1 / 2)), TNPoint(GetX(0), GetY(-H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(H1 / 2)), TNPoint(GetX(0), GetY(H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 32, GetY(0)), "H", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "1", 0, 1);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 2, GetY(S / 2) - 20), "S", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "", 0, 1);
        let oy = GetY(-r + H1 / 2);
        oy = (oy > 350) ? 350 : oy;
        radius(TNPoint(GetX(r * Math.cos(PI * (3.0 / 4 - 1.0 / 8))), GetY(r * Math.sin(PI * (3.0 / 4 - 1.0 / 8)) - r + H1 / 2)),
            TNPoint(GetX(0), oy),
            TNPoint(GetX(r * Math.cos(PI * (3.0 / 4 - 1.0 / 8))), GetY(r * Math.sin(PI * (3.0 / 4 - 1.0 / 8)) - r + H1 / 2) + 30 * Mas.my / 4),
            "R", TNPoint(GetX(r * Math.cos(PI * (3.0 / 4 - 1.0 / 8))) + 10, GetY(r * Math.sin(PI * (3.0 / 4 - 1.0 / 8)) - r + H1 / 2) + 33 * Mas.my / 4), "1");
    }
}

function ploval(H1, BVR, S, b, Nparris) {
    let sina = S / H1, cosa = Math.sqrt(1 - sina * sina);
    let sina1 = (S / 2 + H1 / 10) / (H1 / 2 + H1 / 10), cosa1 = Math.sqrt(1 - sina1 * sina1);
    let x1 = cosa1 * (H1 / 2 + H1 / 10) + BVR / 2 - H1 / 2 * cosa, x2 = cosa1 * H1 / 2 + BVR / 2 - H1 / 2 * cosa, y2 = sina1 * H1 / 2;
    let ttt1 = null;
    for (let i of [1, -1]) {
        for (let i1 of [1, -1]) {
            if ((Nparris & 1) === 1) {
                SetLineMode('Solid2'); MoveTo((-H1 / 4 - BVR / 2) * i1, S / 2 * i, Nparris); LineTo(-x1 * i1, S / 2 * i, Nparris);
                arcs(TFPoint(-x1 * i1, S / 2 * i), TFPoint(-x2 * i1, y2 * i), TFPoint(-x1 * i1, (S / 2 + H1 / 10) * i, H1 / 10), TFPoint(-10000, -10000), Nparris);
                let ttt = arcs(TFPoint(-x2 * i1, y2 * i), TFPoint((-BVR / 2 + cosa * H1 / 2) * i1, H1 / 2 * i), TFPoint((-BVR / 2 + cosa * H1 / 2) * i1, 0, H1 / 2), TFPoint(-10000, -10000), Nparris);
                LineTo(ttt.x, H1 / 2 * i, Nparris); LineTo(0, H1 / 2 * i, Nparris);
                if (i1 < 0) MoveTo(ttt.x, ttt.y, Nparris); else ttt1 = ttt;
                ctx.stroke();
            }
            if ((Nparris & 4) === 4) {
                SetLineMode('SoliDash');
                let y = newPoint(H1 / 2, b, TFPoint(0, 0), TFPoint(BVR / 2, S / 2, 0), TFPoint(BVR / 2 - cosa * H1 / 2, H1 / 2, 0), 5);
                MoveTo(0, H1 / 2 * i, Nparris);
                if (b < BVR / 2 - cosa * H1 / 2) LineTo(-b * i1, H1 / 2 * i, Nparris);
                else if (b < BVR / 2) {
                    LineTo((-BVR / 2 + cosa * H1 / 2) * i1, H1 / 2 * i, Nparris);
                    arcs(TFPoint((-BVR / 2 + cosa * H1 / 2) * i1, H1 / 2 * i), TFPoint(-b * i1, y * i), TFPoint((-BVR / 2 + cosa * H1 / 2) * i1, 0, H1 / 2), TFPoint(-10000, -10000), Nparris);
                } else {
                    LineTo((-BVR / 2 + cosa * H1 / 2) * i1, H1 / 2 * i, Nparris);
                    arcs(TFPoint((-BVR / 2 + cosa * H1 / 2) * i1, H1 / 2 * i), TFPoint(-BVR / 2 * i1, y * i), TFPoint((-BVR / 2 + cosa * H1 / 2) * i1, 0, H1 / 2), TFPoint(-10000, -10000), Nparris);
                    LineTo(-BVR / 2 * i1, S / 2 * i, Nparris); LineTo(-b * i1, S / 2 * i, Nparris);
                }
                MoveTo(-b * i1, y * i, Nparris); LineTo(-b * i1, 0, Nparris); ctx.stroke();
            }
        }
    }
    if ((Nparris & 2) === 2) {
        SetLineMode('SoliDash'); MoveTo(-H1 / 4 - BVR / 2 - 10, 0, Nparris); LineTo(H1 / 4 + BVR / 2 + 10, 0, Nparris);
        MoveTo(0, -H1 / 2 + 10, Nparris); LineTo(0, H1 / 2 - 10, Nparris); ctx.stroke();
        SetLineMode('Solid');
        strela(TNPoint(GetX(-BVR / 2), GetY(S / 2)), TNPoint(GetX(BVR / 2), GetY(S / 2)),
            TNPoint(GetX(-BVR / 2), GetY(H1 / 2) - 30), TNPoint(GetX(BVR / 2), GetY(H1 / 2) - 30),
            TNPoint(GetX(0) - 7, GetY(H1 / 2) - 50), "B", TNPoint(GetX(0) + 2, GetY(H1 / 2) - 48), "вр", 1, 0);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(-H1 / 2)), TNPoint(GetX(0), GetY(-H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(H1 / 2)), TNPoint(GetX(0), GetY(H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 32, GetY(0)), "H", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "1", 0, 1);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 2, GetY(S / 2) - 20), "S", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "", 0, 1);
        radius(TNPoint(GetX(H1 / 2 * Math.cos(PI * 3 / 4) - (BVR - H1 * cosa) / 2), GetY(H1 / 2 * Math.sin(PI * 3 / 4))),
            TNPoint(GetX(-(BVR - H1 * cosa) / 2), GetY(0)),
            TNPoint(GetX(H1 / 2 * Math.cos(PI * 3 / 4) - (BVR - H1 * cosa) / 2), GetY(H1 / 2 * Math.sin(PI * 3 / 4)) + 30 * Mas.my / 4),
            "R", TNPoint(GetX(H1 / 2 * Math.cos(PI * 3 / 4) - (BVR - H1 * cosa) / 2) + 10, GetY(H1 / 2 * Math.sin(PI * 3 / 4)) + 33 * Mas.my / 4), "1");
    }
}

function kvadrat(H1, BVR, S, r, b, Nparris) {
    let H1st = H1, ahr = (H1 - S) / 2 - r, bh = BVR / 2;
    let acr = Math.sqrt(ahr * ahr + bh * bh);
    let alpha1 = Math.asin(r / acr) + Math.asin(ahr / acr);
    H1 = 2 * bh * Math.tan(alpha1) + S;
    for (let i of [1, -1]) {
        if ((Nparris & 1) === 1) {
            MoveTo(-H1 / 4 - BVR / 2, S / 2 * i, Nparris); SetLineMode('Solid2');
            let res = fillet(TFPoint(-H1 / 4 - BVR / 2, S / 2 * i), TFPoint(-BVR / 2, S / 2 * i, H1 / 10), TFPoint(0, H1 / 2 * i));
            LineTo(res.B1.x, res.B1.y, Nparris); arcs(res.B1, res.E1, res.Ctr, TFPoint(res.B1.x, res.B1.y), Nparris);
            let res2 = fillet(TFPoint(-BVR / 2, S / 2 * i), TFPoint(0, H1 / 2 * i, r), TFPoint(BVR / 2, S / 2 * i));
            LineTo(res2.B1.x, res2.B1.y, Nparris);

            ctx.stroke(); SetLineMode('Solid');
            LineTo(0, H1 / 2 * i, Nparris); LineTo(res2.E1.x, res2.E1.y, Nparris);

            ctx.stroke(); SetLineMode('Solid2');
            MoveTo(res2.B1.x, res2.B1.y, Nparris);
            arcs(res2.B1, res2.E1, res2.Ctr, TFPoint(res2.B1.x, res2.B1.y), Nparris);
            LineTo(res2.E1.x, res2.E1.y, Nparris);

            let res3 = fillet(TFPoint(H1 / 4 + BVR / 2, S / 2 * i), TFPoint(BVR / 2, S / 2 * i, H1 / 10), TFPoint(0, H1 / 2 * i));
            LineTo(res3.E1.x, res3.E1.y, Nparris);
            arcs(res3.B1, res3.E1, res3.Ctr, TFPoint(res3.B1.x, res3.B1.y, H1 / 10), Nparris);
            MoveTo(res3.B1.x, res3.B1.y, Nparris); LineTo(H1 / 4 + BVR / 2, S / 2 * i, Nparris); ctx.stroke();
        }
        if ((Nparris & 4) === 4) {
            SetLineMode('SoliDash');
            let tp = TFPoint(r * Math.sin(alpha1), H1st / 2 - r + r * Math.cos(alpha1));
            let y = newPoint(r, b, TFPoint(BVR / 2, S / 2, 0), tp, TFPoint(0, H1 / 2, 0), 3);
            if (b < r * Math.cos(alpha1)) {
                arcs(TFPoint(-b, y * i), TFPoint(b, y * i), TFPoint(0, (H1st / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                MoveTo(-b, y * i, Nparris); LineTo(-b, 0, Nparris); MoveTo(b, y * i, Nparris); LineTo(b, 0, Nparris);
            } else if (b < BVR / 2 && b > r * Math.cos(alpha1)) {
                arcs(TFPoint(-tp.x, tp.y * i), TFPoint(tp.x, tp.y * i), TFPoint(0, (H1st / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                MoveTo(-tp.x, tp.y * i, Nparris); LineTo(-b, y * i, Nparris); LineTo(-b, 0, Nparris);
                MoveTo(tp.x, tp.y * i, Nparris); LineTo(b, y * i, Nparris); LineTo(b, 0, Nparris);
            } else {
                arcs(TFPoint(-tp.x, tp.y * i), TFPoint(tp.x, tp.y * i), TFPoint(0, (H1st / 2 - r) * i, r), TFPoint(-10000, -10000), Nparris);
                MoveTo(-tp.x, tp.y * i, Nparris); LineTo(-BVR / 2, S / 2 * i, Nparris); LineTo(-b, S / 2 * i, Nparris); LineTo(-b, 0, Nparris);
                MoveTo(tp.x, tp.y * i, Nparris); LineTo(BVR / 2, S / 2 * i, Nparris); LineTo(b, S / 2 * i, Nparris); LineTo(b, 0, Nparris);
            }
            ctx.stroke();
        }
    }
    if ((Nparris & 2) === 2) {
        SetLineMode('SoliDash'); MoveTo(-H1 / 4 - BVR / 2 - 10, 0, Nparris); LineTo(H1 / 4 + BVR / 2 + 10, 0, Nparris);
        MoveTo(0, -H1st / 2 + 10, Nparris); LineTo(0, H1st / 2 - 10, Nparris); ctx.stroke();
        SetLineMode('Solid');
        strela(TNPoint(GetX(-BVR / 2), GetY(S / 2)), TNPoint(GetX(BVR / 2), GetY(S / 2)),
            TNPoint(GetX(-BVR / 2), GetY(H1st / 2) - 30), TNPoint(GetX(BVR / 2), GetY(H1st / 2) - 30),
            TNPoint(GetX(0) - 7, GetY(H1st / 2) - 50), "B", TNPoint(GetX(0) + 2, GetY(H1st / 2) - 48), "вр", 1, 0);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(-H1st / 2)), TNPoint(GetX(0), GetY(-H1st / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(H1st / 2)), TNPoint(GetX(0), GetY(H1st / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 32, GetY(0)), "H", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "1", 0, 1);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) + 10, GetY(-S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) + 10, GetY(S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 2, GetY(S / 2) - 20), "S", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "", 0, 1);

        let resR = fillet(TFPoint(-BVR / 2, S / 2), TFPoint(0, H1 / 2, r), TFPoint(BVR / 2, S / 2));
        radius(TNPoint(GetX(resR.Ctr.r * Math.cos(PI * 3 / 4) + resR.Ctr.x), GetY(resR.Ctr.r * Math.sin(PI * 3 / 4) + resR.Ctr.y)),
            TNPoint(GetX(resR.Ctr.x), GetY(resR.Ctr.y)),
            TNPoint(GetX(resR.Ctr.r * Math.cos(PI * 3 / 4) + resR.Ctr.x), GetY(resR.Ctr.r * Math.sin(PI * 3 / 4) + resR.Ctr.y) + 30 * Mas.my / 4),
            "R", TNPoint(GetX(resR.Ctr.r * Math.cos(PI * 3 / 4) + resR.Ctr.x) + 8, GetY(resR.Ctr.r * Math.sin(PI * 3 / 4) + resR.Ctr.y) + 33 * Mas.my / 4), "1");
    }
}

function rebroval(H1, BVR, S, r, b, Nparris) {
    let sina = S / 2 / r, cosa = Math.sqrt(1 - sina * sina);
    let sina1 = (S / 2 + 0.1 * H1) / (r + 0.1 * H1), cosa1 = Math.sqrt(1 - sina1 * sina1);
    let x1 = cosa1 * (r + 0.1 * H1) + BVR / 2 - cosa * r, y2 = sina1 * r, x2 = cosa1 * r + BVR / 2 - cosa * r;
    let ttt1 = null;
    for (let i of [1, -1]) {
        for (let i1 of [1, -1]) {
            if ((Nparris & 1) === 1) {
                SetLineMode('Solid2'); MoveTo((-H1 / 4 - BVR / 2) * i1, S / 2 * i, Nparris); LineTo(-x1 * i1, S / 2 * i, Nparris);
                let ttt = arcs(TFPoint(-x1 * i1, S / 2 * i), TFPoint(-x2 * i1, y2 * i), TFPoint(-x1 * i1, (0.1 * H1 + S / 2) * i, 0.1 * H1), TFPoint(-x1 * i1, S / 2 * i), Nparris);
                ttt = arcs(TFPoint(-x2 * i1, y2 * i), TFPoint(0, H1 / 2 * i), TFPoint((-BVR / 2 + cosa * r) * i1, 0, r), ttt, Nparris);
                if (i1 < 0) { MoveTo(ttt.x, ttt.y, Nparris); LineTo(ttt1.x, ttt1.y, Nparris); } else ttt1 = ttt;
                ctx.stroke();
            }
            if ((Nparris & 4) === 4) {
                SetLineMode('SoliDash');
                let y = newPoint(r, b, TFPoint(0, 0), TFPoint(BVR / 2, S / 2 * i, 0), TFPoint(0, 0, 0), 5);
                if (b < BVR / 2) {
                    let tx = b + (-BVR / 2 + cosa * r); let ty = Math.sqrt(1 - (tx / r) * (tx / r)) * r;
                    MoveTo(-b * i1, ty * i, Nparris); LineTo(-b * i1, 0, Nparris);
                } else {
                    MoveTo(-BVR / 2 * i1, S / 2 * i, Nparris); LineTo(-BVR / 2 * i1, (S / 2) * i, Nparris);
                    LineTo(-b * i1, (S / 2) * i, Nparris); LineTo(-b * i1, 0, Nparris);
                }
                let t_y = Math.sqrt(1 - Math.pow(-b * i1 + (-BVR / 2 + cosa * r) * i1, 2) / (r * r)) * r;
                let ttt_d = arcs(TFPoint(-b * i1, t_y * i), TFPoint(0, H1 / 2 * i), TFPoint((-BVR / 2 + cosa * r) * i1, 0, r), TFPoint(-10000, -10000), Nparris);
                if (i1 < 0) { MoveTo(ttt_d.x, ttt_d.y, Nparris); LineTo(ttt1.x, ttt1.y, Nparris); } else ttt1 = ttt_d;
                ctx.stroke();
            }
        }
    }
    if ((Nparris & 2) === 2) {
        SetLineMode('SoliDash'); MoveTo(-H1 / 4 - BVR / 2 - 10, 0, Nparris); LineTo(H1 / 4 + BVR / 2 + 10, 0, Nparris);
        MoveTo(0, -H1 / 2 + 10, Nparris); LineTo(0, H1 / 2 - 10, Nparris); ctx.stroke();
        SetLineMode('Solid');
        strela(TNPoint(GetX(-BVR / 2), GetY(S / 2)), TNPoint(GetX(BVR / 2), GetY(S / 2)),
            TNPoint(GetX(-BVR / 2), GetY(H1 / 2) - 10), TNPoint(GetX(BVR / 2), GetY(H1 / 2) - 10),
            TNPoint(GetX(0) - 7, GetY(H1 / 2) - 30), "B", TNPoint(GetX(0) + 2, GetY(H1 / 2) - 28), "вр", 1, 0);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(-H1 / 2)), TNPoint(GetX(0), GetY(-H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(H1 / 2)), TNPoint(GetX(0), GetY(H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 32, GetY(0)), "H", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "1", 0, 1);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 2, GetY(S / 2) - 20), "S", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(S / 2) - 15), "", 0, 1);

        radius(TNPoint(GetX((-BVR / 2 + cosa * r + r * Math.cos(PI * 3 / 4))), GetY(-r * Math.sin(PI * 3 / 4))),
            TNPoint(GetX((-BVR / 2 + cosa * r)), GetY(0)),
            TNPoint(GetX((-BVR / 2 + cosa * r + r * Math.cos(PI * 3 / 4))) - 10 + 4, GetY(-r * Math.sin(PI * 3 / 4)) - 22 - 5),
            "R", TNPoint(GetX((-BVR / 2 + cosa * r + r * Math.cos(PI * 3 / 4))) + 8 - 8 + 4, GetY(-r * Math.sin(PI * 3 / 4)) - 20 - 5), "1");
        radius(TNPoint(GetX(-(-BVR / 2 + cosa * r + r * Math.cos(PI * 3 / 4))), GetY(r * Math.sin(PI * 3 / 4))),
            TNPoint(GetX(-(-BVR / 2 + cosa * r)), GetY(0)),
            TNPoint(GetX(-(-BVR / 2 + cosa * r + r * Math.cos(PI * 3 / 4))) - 28 - 10 - 4, GetY(r * Math.sin(PI * 3 / 4)) + 8),
            "R", TNPoint(GetX(-(-BVR / 2 + cosa * r + r * Math.cos(PI * 3 / 4))) - 20 - 8 - 4, GetY(r * Math.sin(PI * 3 / 4)) + 10), "1");
    }
}

function shestugol(H1, BVR, BD, S, r, rv, delta, b, Nparris) {
    for (let i of [1, -1]) {
        for (let i1 of [1, -1]) {
            if ((Nparris & 1) === 1) {
                SetLineMode('Solid2'); MoveTo((-H1 / 4 - BVR / 2) * i1, S / 2 * i, Nparris);
                let res = fillet(TFPoint((-H1 / 4 - BVR / 2) * i1, S / 2 * i), TFPoint(-BVR / 2 * i1, S / 2 * i, Math.trunc(H1 / 10)), TFPoint(-BD / 2 * i1, H1 / 2 * i));
                LineTo(res.B1.x, res.B1.y, Nparris); arcs(res.B1, res.E1, res.Ctr, TFPoint(-10000, -10000), Nparris);
                if (Math.abs(delta) < 0.01) {
                    let res2 = fillet(TFPoint(res.E1.x, res.E1.y), TFPoint(-BD / 2 * i1, H1 / 2 * i, r), TFPoint(0, H1 / 2 * i));
                    LineTo(res2.B1.x, res2.B1.y, Nparris); arcs(res2.B1, res2.E1, res2.Ctr, TFPoint(-10000, -10000), Nparris);
                    LineTo(res2.E1.x, res2.E1.y, Nparris); LineTo(0, res2.E1.y, Nparris);
                } else {
                    let res2 = fillet(TFPoint(res.E1.x, res.E1.y), TFPoint(-BD / 2 * i1, H1 / 2 * i, r), TFPoint(0, H1 / 2 * i));
                    LineTo(res2.B1.x, res2.B1.y, Nparris);
                    let R_ = ((res2.Ctr.r - delta) * (res2.Ctr.r - delta) + res2.Ctr.x * res2.Ctr.x) / (2 * delta);
                    let yo = R_ - delta + H1 / 2, x1 = R_ * Math.abs(res2.Ctr.x) / (r + R_);
                    let y1 = Math.sqrt(res2.Ctr.r * res2.Ctr.r - Math.pow(Math.abs(res2.Ctr.x) - x1, 2)) + Math.abs(res2.Ctr.y);
                    arcs(res2.B1, TFPoint(-x1 * i1, y1 * i), res2.Ctr, TFPoint(-10000, -10000), Nparris);
                    arcs(TFPoint(-x1 * i1, y1 * i), TFPoint(0, (H1 / 2 - delta) * i), TFPoint(0, yo * i, R_), TFPoint(-10000, -10000), Nparris);
                }
                ctx.stroke();
            }
            if ((Nparris & 4) === 4) {
                SetLineMode('SoliDash');
                let res = fillet(TFPoint(0, H1 / 2), TFPoint(BD / 2, H1 / 2, r), TFPoint(BVR / 2, S / 2));
                let y = newPoint(r, b, TFPoint(BVR / 2, S / 2, 0), res.E1, res.B1, 8);
                MoveTo(0, H1 / 2 * i, Nparris);
                if (b < res.B1.x) LineTo(-b * i1, H1 / 2 * i, Nparris);
                else {
                    if (Math.abs(delta) < 0.01) LineTo(-res.B1.x * i1, res.B1.y * i, Nparris);
                    else {
                        let R_ = ((res.Ctr.r - delta) * (res.Ctr.r - delta) + res.Ctr.x * res.Ctr.x) / (2 * delta);
                        let yo = R_ - delta + H1 / 2, x1 = R_ * Math.abs(res.Ctr.x) / (r + R_);
                        let y1 = Math.sqrt(res.Ctr.r * res.Ctr.r - Math.pow(Math.abs(res.Ctr.x) - x1, 2)) + Math.abs(res.Ctr.y);
                        MoveTo(0, (H1 / 2 - delta) * i, Nparris);
                        arcs(TFPoint(0, (H1 / 2 - delta) * i), TFPoint(-x1 * i1, y1 * i), TFPoint(0, yo * i, R_), TFPoint(0, (H1 / 2 - delta) * i), Nparris);
                        LineTo(-x1 * i1, y1 * i, Nparris); res.B1.x = x1; res.B1.y = y1;
                    }
                    if (b < res.E1.x) {
                        arcs(TFPoint(-res.B1.x * i1, res.B1.y * i), TFPoint(-b * i1, y * i), TFPoint(-res.Ctr.x * i1, res.Ctr.y * i, r), TFPoint(-10000, -10000), Nparris);
                        MoveTo(-b * i1, y * i, Nparris);
                    } else if (b < BVR / 2) {
                        arcs(TFPoint(-res.B1.x * i1, res.B1.y * i), TFPoint(-res.E1.x * i1, res.E1.y * i), TFPoint(-res.Ctr.x * i1, res.Ctr.y * i, r), TFPoint(-10000, -10000), Nparris);
                        MoveTo(-res.E1.x * i1, res.E1.y * i, Nparris); LineTo(-b * i1, y * i, Nparris);
                    } else {
                        arcs(TFPoint(-res.B1.x * i1, res.B1.y * i), TFPoint(-res.E1.x * i1, res.E1.y * i), TFPoint(-res.Ctr.x * i1, res.Ctr.y * i, r), TFPoint(-10000, -10000), Nparris);
                        MoveTo(-res.E1.x * i1, res.E1.y * i, Nparris); LineTo(-BVR / 2 * i1, S / 2 * i, Nparris); LineTo(-b * i1, S / 2 * i, Nparris);
                    }
                }
                LineTo(-b * i1, 0, Nparris); ctx.stroke();
            }
        }
    }
    if ((Nparris & 2) === 2) {
        SetLineMode('SoliDash'); MoveTo(-H1 / 4 - BVR / 2 - 10, 0, Nparris); LineTo(H1 / 4 + BVR / 2 + 10, 0, Nparris);
        MoveTo(0, -H1 / 2 + 10, Nparris); LineTo(0, H1 / 2 - 10, Nparris); ctx.stroke();
        SetLineMode('Solid');
        strela(TNPoint(GetX(-BVR / 2), GetY(S / 2)), TNPoint(GetX(BVR / 2), GetY(S / 2)),
            TNPoint(GetX(-BVR / 2), GetY(H1 / 2) - 30), TNPoint(GetX(BVR / 2), GetY(H1 / 2) - 30),
            TNPoint(GetX(0) - 7, GetY(H1 / 2) - 50), "B", TNPoint(GetX(0) + 2, GetY(H1 / 2) - 47), "вр", 1, 0);
        strela(TNPoint(GetX(-BD / 2), GetY(H1 / 2 - 0.14 * r)), TNPoint(GetX(BD / 2), GetY(H1 / 2 - 0.14 * r)),
            TNPoint(GetX(-BD / 2), GetY(H1 / 2) - 10), TNPoint(GetX(BD / 2), GetY(H1 / 2) - 10),
            TNPoint(GetX(0) - 7, GetY(H1 / 2) - 29), "B", TNPoint(GetX(0) + 2, GetY(H1 / 2) - 26), "д", 1, 0);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(-H1 / 2)), TNPoint(GetX(0), GetY(-H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(H1 / 2)), TNPoint(GetX(0), GetY(H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 32, GetY(0)), "H", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "1", 0, 1);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 2, GetY(S / 2) - 20), "S", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "", 0, 1);
        let resR = fillet(TFPoint(-BVR / 2, S / 2), TFPoint(-BD / 2, H1 / 2, r), TFPoint(0, H1 / 2));
        radius(TNPoint(GetX(resR.Ctr.x + r * Math.cos(PI * 3 / 4)), GetY(resR.Ctr.y + r * Math.sin(PI * 3 / 4))),
            TNPoint(GetX(resR.Ctr.x), GetY(resR.Ctr.y)),
            TNPoint(GetX(resR.Ctr.x) + 4, GetY(resR.Ctr.y)),
            "R", TNPoint(GetX(resR.Ctr.x) + 14, GetY(resR.Ctr.y) + 2), "1");
    }
}

function vertshug(H1, BVR, S, b, Nparris) {
    for (let i of [1, -1]) {
        for (let i1 of [1, -1]) {
            if ((Nparris & 1) === 1) {
                SetLineMode('Solid2'); let r = H1 / 2.31, y1 = r * Math.sin(PI / 6) / Math.cos(PI / 6);
                MoveTo((-H1 / 4 - BVR / 2) * i1, S / 2 * i, Nparris);
                let res = fillet(TFPoint((-H1 / 4 - BVR / 2) * i1, S / 2 * i), TFPoint(-BVR / 2 * i1, S / 2 * i, H1 / 20), TFPoint(-r * i1, y1 * i));
                LineTo(res.B1.x, res.B1.y, Nparris); arcs(res.B1, res.E1, res.Ctr, TFPoint(-10000, -10000), Nparris);
                MoveTo(res.E1.x, res.E1.y, Nparris); LineTo(-r * i1, y1 * i, Nparris); LineTo(0, H1 / 2 * i, Nparris);
                ctx.stroke();
            }
            if ((Nparris & 4) === 4) {
                let r = H1 / 2.31; SetLineMode('SoliDash');
                let y = newPoint(r, b, TFPoint(BVR / 2, S / 2), TFPoint(r, H1 / 4), TFPoint(0, H1 / 2), 6);
                MoveTo(0, H1 / 2 * i, Nparris);
                if (Math.abs(b) < Math.abs(H1 / 4) || Math.abs(b) < Math.abs(r)) LineTo(-b * i1, y * i, Nparris);
                else if (Math.abs(b) < Math.abs(BVR / 2)) { LineTo(-r * i1, H1 / 4 * i, Nparris); LineTo(-b * i1, y * i, Nparris); }
                else if (Math.abs(b) > Math.abs(BVR / 2) - 0.00001) { LineTo(-r * i1, H1 / 4 * i, Nparris); LineTo(-BVR / 2 * i1, S / 2 * i, Nparris); LineTo(-b * i1, y * i, Nparris); }
                LineTo(-b * i1, 0, Nparris); ctx.stroke();
            }
        }
    }
    if ((Nparris & 2) === 2) {
        SetLineMode('SoliDash'); MoveTo(-H1 / 4 - BVR / 2 - 10, 0, Nparris); LineTo(H1 / 4 + BVR / 2 + 10, 0, Nparris);
        MoveTo(0, -H1 / 2 + 10, Nparris); LineTo(0, H1 / 2 - 10, Nparris); ctx.stroke();
        SetLineMode('Solid');
        strela(TNPoint(GetX(-BVR / 2), GetY(S / 2)), TNPoint(GetX(BVR / 2), GetY(S / 2)),
            TNPoint(GetX(-BVR / 2), GetY(H1 / 2) - 30), TNPoint(GetX(BVR / 2), GetY(H1 / 2) - 30),
            TNPoint(GetX(0) - 7, GetY(H1 / 2) - 50), "B", TNPoint(GetX(0) + 2, GetY(H1 / 2) - 47), "вр", 1, 0);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(-H1 / 2)), TNPoint(GetX(0), GetY(-H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 10, GetY(H1 / 2)), TNPoint(GetX(0), GetY(H1 / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 32, GetY(0)), "H", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "1", 0, 1);
        strela(TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(-S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)), TNPoint(GetX(-BVR / 2 - H1 / 4) + 8, GetY(S / 2)),
            TNPoint(GetX(-BVR / 2 - H1 / 4) - 10 + 8, GetY(S / 2) - 20), "S", TNPoint(GetX(-BVR / 2 - H1 / 4) - 21, GetY(0) + 5), "", 0, 1);
    }
}

// --- ГЛАВНАЯ ФУНКЦИЯ DRAW ---
function drawProfile() {
    ctx.clearRect(0, 0, 700, 550);
    ctx.fillStyle = "#f8f9fa"; ctx.fillRect(0, 0, 700, 550);
    ctx.strokeStyle = "#ccc"; ctx.lineWidth = 1; ctx.setLineDash([5, 5]);
    ctx.beginPath();
    ctx.moveTo(0, center.y); ctx.lineTo(canvas.width, center.y);
    ctx.moveTo(center.x, 0); ctx.lineTo(center.x, canvas.height);
    ctx.stroke();

    // КАЛИБР
    if (pData.KOD1 > 0) {
        switch (pData.KOD1) {
            case 1: case 0: bochka(pData.H1, pData.B1, 0, 1 | 2); break;
            case 2: krug(pData.H1, pData.BVR, pData.S, pData.B1 / 2, 1 | 2); break;
            case 3: case 7: case 12: kvadrat(pData.H1, pData.BVR, pData.S, pData.R, pData.BVR / 8, 1 | 2); break;
            case 4: oval(pData.H1, pData.BVR, pData.BD, pData.S, pData.ROV, pData.BVR / 8, 1 | 2); break;
            case 5: ploval(pData.H1, pData.BVR, pData.S, pData.BVR / 8, 1 | 2); break;
            case 6: case 8: case 11: shestugol(pData.H1, pData.BVR, pData.BD, pData.S, pData.R, pData.ROV, pData.R8, pData.BVR / 8, 1 | 2); break;
            case 9: rebroval(pData.H1, pData.BVR, pData.S, pData.ROV, pData.BVR / 8, 1 | 2); break;
            case 10: vertshug(pData.H1, pData.BVR, pData.S, pData.BVR / 8, 1 | 2); break;
        }
    }

    // ДАННЫЕ ПОДКАТА
    if (pData.KOD0 > 0) {
        let mH = pData.H0, mBVR = pData.B0, mS = 2, mR = 1.0, mROV = 0.5, mBD = 0, mDelta = 0.5, mB1 = 0;
        let type_p = 4 + isKantov;

        switch (pData.KOD0) {
            case 11: case 8: case 6:
                if (isKantov === 8) { mH = pData.B0; mBVR = pData.H0; mBD = pData.H0 - (pData.B0 - mS) * (pData.KOD0 === 8 ? 0.3 : 0.1); if (pData.KOD0 === 6) mBD = pData.H0 - (pData.B0 - mS) * 1; mROV = (pData.KOD0 === 8 ? 0 : 0.5); mDelta = (pData.KOD0 === 8 ? 0 : 0.5); }
                else { mH = pData.H0; mBVR = pData.B0; mBD = pData.B0 - (mH - mS) * (pData.KOD0 === 8 ? 0.1 : 0.1); if (pData.KOD0 === 6) mBD = pData.B0 - (mH - mS) * 1; mROV = (pData.KOD0 === 8 ? 0 : 0.5); mDelta = (pData.KOD0 === 8 ? 0 : 0.5); }
                mB1 = mBVR * 0.95;
                edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
                shestugol(mH, mBVR, mBD, mS, mR, mROV, mDelta, getDrawWidthForPodkat(mB1), type_p);
                break;
            case 10:
                if (isKantov === 8) { mH = pData.B0; mBVR = pData.H0; mBD = mH * Math.sqrt(3 / 2); mB1 = mBVR; }
                else { mH = pData.H0; mBVR = pData.B0; mB1 = mBVR * 0.95; mBD = mB1 * Math.sqrt(3 / 2); }
                edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
                vertshug(mH, mBVR, mS, getDrawWidthForPodkat(mB1), type_p);
                break;
            case 2:
                if (isKantov === 8) { mH = pData.H0; mBVR = pData.H0; mB1 = mBVR; }
                else { mH = pData.H0; mBVR = pData.H0; mB1 = mBVR * 0.95; }
                edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
                krug(mH, mBVR, mS, getDrawWidthForPodkat(mB1), type_p);
                break;
            //case 3:
            //    if (isKantov === 8) { mR = 0.15 * mH; mH = pData.H0; mBVR = pData.B0; mB1 = mBVR * 0.95; }
            //    else if (isKantov === 16) { mH = Math.sqrt(pData.H0 * pData.H0 / 4 + pData.B0 * pData.B0 / 4); mBVR = mH; mR = 0.15 * mH; mB1 = mBVR * 0.95; }
            //    else { mH = pData.B0; mBVR = pData.H0; mR = 0.15 * mH; mB1 = mBVR * 0.95; }
            //    edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
            //    kvadrat(mH, mBVR, mS, mR, getDrawWidth(mB1), type_p);
            //    break;
            case 3:
                if (isKantov === 8) { mR = 0.15 * mH; mH = pData.H0; mBVR = pData.B0; mB1 = mBVR * 0.95; }
                else if (isKantov === 16) { mH = Math.sqrt(pData.H0 * pData.H0 / 2 + pData.B0 * pData.B0 / 2) * Math.sqrt(2); mBVR = mH; mR = 0; mB1 = mBVR * 0.95; mS = 0; }
                else { mH = pData.B0; mBVR = pData.H0; mR = 0.15 * mH; mB1 = mBVR * 0.95; }
                edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
                kvadrat(mH, mBVR, mS, mR, getDrawWidth(mB1), type_p);
                break;
            case 4:
                if (isKantov === 8) { mH = pData.B0; mBVR = pData.H0; mBD = 0; mROV = (mBVR / 2 * mBVR / 2 + (mH - mS) * (mH - mS) / 4) / (mH - mS); mB1 = mBVR * 0.95; }
                else { mH = pData.H0; mBVR = pData.B0; mBD = 0; mROV = (mBVR / 2 * mBVR / 2 + (mH - mS) * (mH - mS) / 4) / (mH - mS); mB1 = mBVR * 0.95; }
                edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
                oval(mH, mBVR, mBD, mS, mROV, getDrawWidth(mB1), type_p);
                break;
            case 5:
                if (isKantov === 8) { mH = pData.B0; mBVR = pData.H0; mB1 = mBVR; }
                else { mH = pData.H0; mBVR = pData.B0; mB1 = mBVR * 0.95; }
                edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
                ploval(mH, mBVR, mS, getDrawWidth(mB1), type_p);
                break;
            case 7:
                if (isKantov === 8) { mH = pData.B0; mBVR = pData.H0; mR = 0.15 * mH; mB1 = mBVR * 0.95; }
                else if (isKantov === 16) { mH = Math.sqrt(pData.H0 * pData.H0 / 4 + pData.B0 * pData.B0 / 4); mBVR = mH; mR = 0.15 * mH; mB1 = mBVR * 0.95; }
                else { mH = pData.H0; mBVR = pData.B0; mR = 0.15 * mH; mB1 = mBVR * 0.95; }
                edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
                kvadrat(mH, mBVR, mS, mR, getDrawWidth(mB1), type_p);
                break;
            case 9:
                if (isKantov === 8) { mH = pData.B0; mBVR = pData.H0; mROV = Math.sqrt(mH * mH / 4 + Math.pow((mH * mH / 4 - mS * mS / 4 - mBVR * mBVR / 4) / mBVR, 2)); mB1 = mBVR * 0.95; }
                else { mH = pData.H0; mBVR = pData.B0; mROV = Math.sqrt(mH * mH / 4 + Math.pow((mH * mH / 4 - mS * mS - mBVR * mBVR / 4) / mBVR, 2)); mB1 = mBVR; }
                edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
                rebroval(mH, mBVR, mS, mROV, getDrawWidth(mB1), type_p);
                break;
            case 1: case 0: case 12:
                if (isKantov === 8) { mH = pData.B0; mB1 = pData.H0; }
                else { mH = pData.H0; mB1 = pData.B0; }
                edgeBand(type_p, pData.KOD0, pData.B1, mH, mBVR, pData.H1);
                bochka(mH, mB1, 0, type_p);
                break;
        }
    }
}

// Вспомогательная функция для ширины отрисовки подката
function getDrawWidth(mB1) {
    if ((isKantov & 8) === 8) return pData.H0 / 2;
    if ((isKantov & 16) === 16) return mB1 / 2;
    return pData.B0 / 2;
}

function determineKantovka() {
    isKantov = 0;
    let K_curr = pData.KOD1;
    let K_prev = pData.KOD0;
    let H_curr = pData.H1;
    let H_prev = pData.H0;

    if (pData.N != 1 && (K_prev === 3 || K_prev === 12)) {
        isKantov = 16;
    } else {
        switch (K_curr) {
            case 2:
                if (K_prev === 4 || K_prev === 5 || K_prev === 1) isKantov = 8;
                break;
            case 3:
                if (K_prev === 4 || K_prev === 8 || K_prev === 7) isKantov = 8;
                break;
            case 10:
                if (K_prev === 6 || K_prev === 8) isKantov = 8;
                break;
            case 1:
                if (H_prev < H_curr) isKantov = 8;
                if (K_prev === 3) isKantov = 16;
                if ((K_prev === 8 || K_prev === 1 || K_prev === 5) && H_curr > H_prev) isKantov = 8;
                break;
            case 8:
                if (H_prev < H_curr) isKantov = 8;
                if (K_prev === 8 && H_curr > H_prev) isKantov = 8;
                if (K_prev === 1 && H_curr > H_prev) isKantov = 8;
                if (K_prev === 3) isKantov = 16;
                if (K_prev === 10) isKantov = 8;
                break;
            case 9:
                if (K_prev === 4 || K_prev === 8) isKantov = 8;
                break;
            case 7:
                if (K_prev === 7) isKantov = 8;
                if (K_prev === 8 || K_prev === 1) isKantov = 16;
                break;
            case 4:
                if (K_prev === 4 || K_prev === 9) isKantov = 8;
                if (K_prev === 3) isKantov = 16;
                if ((K_prev === 8 || K_prev === 1) && H_curr > H_prev) isKantov = 8;
                break;
            case 5:
                if (K_prev === 3) isKantov = 16;
                if ((K_prev === 8 || K_prev === 1) && H_curr > H_prev) isKantov = 8;
                if (K_prev === 10) isKantov = 8;
                break;
        }
    }
}