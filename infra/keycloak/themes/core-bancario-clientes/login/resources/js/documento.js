/*
 * Tema core-bancario-clientes: lo que el navegador hace con el documento del cliente.
 *
 * 1. Pantalla de ingreso (RN-17, CL-02, CA-12): agrega un selector de tipo de documento y, al enviar, escribe en el
 *    campo "username" el documento ya normalizado, igual que el dominio (AccesoDigital.NombreDeUsuarioDe):
 *    "CC" + "1.234.567" -> "CC1234567".
 * 2. Pantalla de cambio de clave (CL-06): cancela el envio si la clave nueva contiene el numero de documento.
 *
 * Las funciones normalizarUsuario y contieneNumeroDeDocumento son puras y globales a proposito: las pruebas
 * (Jint) las ejecutan sin navegador. Lo que toca el DOM (iniciar...) solo se comprueba a mano: hueco aceptado.
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

    formulario.addEventListener('submit', function () {
        campo.value = normalizarUsuario(selector.value, campo.value);
    });
}

function iniciarPantallaDeCambioDeClave(formulario) {
    var usuario = formulario.getAttribute('data-usuario');
    var mensaje = formulario.getAttribute('data-mensaje-documento');
    var claveNueva = formulario.querySelector('input[name="password-new"]');
    if (!usuario || !claveNueva) {
        return;
    }

    formulario.addEventListener('submit', function (evento) {
        var aviso = document.getElementById('aviso-numero-de-documento');
        if (aviso) {
            aviso.parentNode.removeChild(aviso);
        }

        if (!contieneNumeroDeDocumento(usuario, claveNueva.value)) {
            return;
        }

        evento.preventDefault();
        evento.stopImmediatePropagation();

        // El formulario deshabilita el boton al enviar (onsubmit en la plantilla); se vuelve a habilitar.
        var boton = formulario.elements['login'];
        if (boton) {
            boton.disabled = false;
        }

        aviso = document.createElement('span');
        aviso.id = 'aviso-numero-de-documento';
        aviso.className = 'pf-v5-c-helper-text__item-text pf-m-error kc-feedback-text';
        aviso.setAttribute('aria-live', 'polite');
        aviso.textContent = mensaje || 'La clave no puede contener tu número de documento.';
        claveNueva.closest('.pf-v5-c-form__group').appendChild(aviso);
    });
}

function iniciarDocumento() {
    var ingreso = document.getElementById('kc-form-login');
    if (ingreso) {
        iniciarPantallaDeIngreso(ingreso);
    }

    var cambioDeClave = document.getElementById('kc-passwd-update-form');
    if (cambioDeClave) {
        iniciarPantallaDeCambioDeClave(cambioDeClave);
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', iniciarDocumento);
} else {
    iniciarDocumento();
}
