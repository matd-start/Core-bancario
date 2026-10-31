/*
 * Tema core-bancario-clientes: lo que el navegador hace con el documento del cliente.
 *
 * 1. Pantalla de ingreso (RN-17, CL-02, CA-12): agrega un selector de tipo de documento y, al enviar, escribe en el
 *    campo "username" el documento ya normalizado, igual que el dominio (AccesoDigital.NombreDeUsuarioDe):
 *    "CC" + "1.234.567" -> "CC1234567".
 * 2. Pantalla de cambio de clave (CL-06): cancela el envio si la clave nueva contiene el numero de documento.
 *
 * ENVIO DELEGADO: este script corre en el head (scripts= de theme.properties), cuando los formularios aun no existen,
 * y DOMContentLoaded espera a los modulos de keycloak.v2 (authChecker.js). En ese hueco el formulario ya se ve y se
 * puede enviar. Por eso el submit se escucha en document, en fase de captura, desde que el script corre: el evento
 * pasa por document antes de llegar al formulario, exista este o no cuando se registro el listener. En
 * DOMContentLoaded solo se agrega el selector; si el envio llega antes, se usa el tipo que el selector mostrara.
 *
 * Las funciones normalizarUsuario y contieneNumeroDeDocumento son puras y globales a proposito: las pruebas
 * (Jint) las ejecutan sin navegador. Lo que toca el DOM se prueba sobre un DOM de mentira (PaginaDelTema), no en
 * un navegador real: hueco aceptado (ADR-0018).
 *
 * LIMITE ACEPTADO: el aviso del numero de documento se puede saltar (sin JavaScript, o con una peticion hecha a
 * mano). Se acepta porque esa regla protege al usuario de si mismo, no frente a un atacante. Lo que el servidor
 * SI garantiza es notContainsUsername: la clave no puede contener el usuario completo (por ejemplo "CC1234567").
 */

var TIPOS_DE_DOCUMENTO = [
    { codigo: 'CC', texto: 'CC - Cédula de ciudadanía' },
    { codigo: 'CE', texto: 'CE - Cédula de extranjería' },
    { codigo: 'PA', texto: 'PA - Pasaporte' }
];

/** Quita espacios, '.' y '-', y pasa a mayusculas SOLO las letras ASCII (igual que Documento en el dominio). */
function normalizarNumero(numero) {
    return String(numero)
        .replace(/[\s.\-]/g, '')
        .replace(/[a-z]/g, function (letra) { return letra.toUpperCase(); });
}

/** El nombre de usuario de Keycloak: tipo + numero normalizado, sin separador. */
function normalizarUsuario(tipo, numero) {
    return String(tipo).toUpperCase() + normalizarNumero(numero);
}

/** true si la clave contiene el numero del documento (el usuario sin sus dos letras de tipo). */
function contieneNumeroDeDocumento(usuario, clave) {
    var numero = normalizarNumero(String(usuario).substring(2));
    if (numero.length === 0) {
        return false;
    }

    var claveEnMayusculas = String(clave).replace(/[a-z]/g, function (letra) { return letra.toUpperCase(); });
    return claveEnMayusculas.indexOf(numero) !== -1;
}

/** Si el campo ya trae un usuario normalizado (tras un intento fallido), lo separa en tipo y numero. */
function separarUsuario(valor) {
    var coincidencia = /^(CC|CE|PA)(.+)$/i.exec(String(valor || ''));
    return coincidencia ? { tipo: coincidencia[1].toUpperCase(), numero: coincidencia[2] } : null;
}

function iniciarPantallaDeIngreso(formulario) {
    var campo = formulario.querySelector('input[name="username"]');
    if (!campo) {
        return;
    }

    var selector = document.createElement('select');
    selector.id = 'tipo-documento';
    selector.className = 'pf-v5-c-form-control';
    TIPOS_DE_DOCUMENTO.forEach(function (tipo) {
        var opcion = document.createElement('option');
        opcion.value = tipo.codigo;
        opcion.textContent = tipo.texto;
        selector.appendChild(opcion);
    });

    var grupo = document.createElement('div');
    grupo.className = 'pf-v5-c-form__group';
    grupo.innerHTML =
        '<div class="pf-v5-c-form__group-label pf-v5-u-pb-xs">' +
        '<label class="pf-v5-c-form__label" for="tipo-documento">' +
        '<span class="pf-v5-c-form__label-text">Tipo de documento</span></label></div>' +
        '<div class="pf-v5-c-form__group-control"></div>';
    grupo.lastChild.appendChild(selector);

    var grupoDelCampo = campo.closest('.pf-v5-c-form__group') || campo.parentNode;
    grupoDelCampo.parentNode.insertBefore(grupo, grupoDelCampo);

    var anterior = separarUsuario(campo.value);
    if (anterior) {
        selector.value = anterior.tipo;
        campo.value = anterior.numero;
    }
}

function normalizarAlEnviar(formulario) {
    var campo = formulario.querySelector('input[name="username"]');
    if (!campo) {
        return;
    }

    var selector = document.getElementById('tipo-documento');
    if (selector) {
        campo.value = normalizarUsuario(selector.value, campo.value);
        return;
    }

    // Envio antes de DOMContentLoaded: si el campo ya trae el tipo (intento fallido, o la persona lo escribio) se usa
    // ese; si no, el que el selector mostrara por defecto. Sin selector no hay otra forma de saber si era CE o PA.
    var escrito = separarUsuario(campo.value);
    campo.value = escrito
        ? normalizarUsuario(escrito.tipo, escrito.numero)
        : normalizarUsuario(TIPOS_DE_DOCUMENTO[0].codigo, campo.value);
}

function avisarSiContieneElNumero(formulario, evento) {
    var usuario = formulario.getAttribute('data-usuario');
    var mensaje = formulario.getAttribute('data-mensaje-documento');
    var claveNueva = formulario.querySelector('input[name="password-new"]');
    if (!usuario || !claveNueva) {
        return;
    }

    var aviso = document.getElementById('aviso-numero-de-documento');
    if (aviso) {
        aviso.parentNode.removeChild(aviso);
    }

    if (!contieneNumeroDeDocumento(usuario, claveNueva.value)) {
        return;
    }

    // Se cancela el envio y el evento no llega al formulario: asi el onsubmit de la plantilla no deshabilita el boton
    // y la persona puede corregir la clave y volver a enviar.
    evento.preventDefault();
    evento.stopImmediatePropagation();

    aviso = document.createElement('span');
    aviso.id = 'aviso-numero-de-documento';
    aviso.className = 'pf-v5-c-helper-text__item-text pf-m-error kc-feedback-text';
    aviso.setAttribute('aria-live', 'polite');
    aviso.textContent = mensaje || 'La clave no puede contener tu número de documento.';
    claveNueva.closest('.pf-v5-c-form__group').appendChild(aviso);
}

function alEnviar(evento) {
    var formulario = evento.target;
    if (!formulario) {
        return;
    }

    if (formulario.id === 'kc-form-login') {
        normalizarAlEnviar(formulario);
    } else if (formulario.id === 'kc-passwd-update-form') {
        avisarSiContieneElNumero(formulario, evento);
    }
}

function iniciarDocumento() {
    var ingreso = document.getElementById('kc-form-login');
    if (ingreso) {
        iniciarPantallaDeIngreso(ingreso);
    }
}

document.addEventListener('submit', alEnviar, true);

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', iniciarDocumento);
} else {
    iniciarDocumento();
}
