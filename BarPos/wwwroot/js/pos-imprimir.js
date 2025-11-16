// ===========================
//    CONEXIÓN QZ TRAY
// ===========================
async function conectarQZ() {
    if (qz.websocket.isActive()) return;
    await qz.websocket.connect();
}

// ===========================
//  OBTENER IMPRESORA POS
// ===========================
async function obtenerImpresoraPOS() {

    let impresoraGuardada = localStorage.getItem("impresoraPOS");

    if (impresoraGuardada)
        return impresoraGuardada;

    const impresoras = await qz.printers.find();

    if (!impresoras || impresoras.length === 0)
        throw new Error("No se encontraron impresoras.");

    const termicas = impresoras.filter(p =>
        p.toLowerCase().includes("pos") ||
        p.toLowerCase().includes("80") ||
        p.toLowerCase().includes("thermal") ||
        p.toLowerCase().includes("printer") ||
        p.toLowerCase().includes("xp")
    );

    let seleccion;

    if (termicas.length === 1) {
        seleccion = termicas[0];
    } else {
        seleccion = prompt(
            "Seleccione la impresora térmica:\n\n" +
            impresoras.map((p, i) => `${i + 1}. ${p}`).join("\n")
        );

        const index = parseInt(seleccion) - 1;

        if (isNaN(index) || index < 0 || index >= impresoras.length)
            throw new Error("Selección inválida.");

        seleccion = impresoras[index];
    }

    localStorage.setItem("impresoraPOS", seleccion);
    return seleccion;
}

// ===========================
//  OBTENER RECIBO BASE64
// ===========================
async function obtenerReciboBase64(id) {
    const resp = await fetch(`/api/impresion/cuenta/${id}`);
    const json = await resp.json();

    if (!json.success)
        throw new Error(json.error || "No se pudo obtener el recibo.");

    return json.data;
}

// ===========================
//  IMPRIMIR CUENTA (FACTURA)
// ===========================
async function imprimirCuenta(id) {
    await conectarQZ();

    const impresora = await obtenerImpresoraPOS();
    const config = qz.configs.create(impresora);

    const base64 = await obtenerReciboBase64(id);

    const data = [{
        type: 'raw',
        format: 'base64',
        data: base64
    }];

    await qz.print(config, data);
}
