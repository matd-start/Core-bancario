using Jint;

namespace CoreBancario.Keycloak.Tests;

/// <summary>
/// Una pantalla del tema core-bancario-clientes con documento.js cargado sobre un DOM de mentira (Jint) que sí guarda
/// los listeners y simula el envío: primero la captura en document, después el propio formulario. Sirve para probar
/// el orden real de una página de Keycloak: el script corre en el head, antes de que exista el formulario, y la
/// persona puede enviar antes de DOMContentLoaded. No es un navegador: no hay HTML, CSS ni red (ADR-0018).
/// </summary>
internal sealed class PaginaDelTema
{
    private const string IdDeIngreso = "kc-form-login";
    private const string IdDeCambioDeClave = "kc-passwd-update-form";

    /// <summary>
    /// El document de mentira, con readyState "loading" y sin formularios. ScriptDelTema también lo usa para cargar
    /// el script, así que hay un solo fake que mantener.
    /// </summary>
    internal const string DomDeMentira = """
        var window = this;
        var __elementos = [];
        var __oyentesDelDocumento = [];

        function __elemento(etiqueta) {
            var el = {
                tagName: String(etiqueta || 'div').toUpperCase(),
                id: '', name: '', className: '', value: '', textContent: '', innerHTML: '', disabled: false,
                hijos: [], parentNode: null, oyentes: [], atributos: {},
                appendChild: function (hijo) {
                    hijo.parentNode = this;
                    this.hijos.push(hijo);
                    if (this.tagName === 'SELECT' && this.value === '') { this.value = hijo.value; }
                    return hijo;
                },
                insertBefore: function (nuevo, referencia) {
                    nuevo.parentNode = this;
                    var i = this.hijos.indexOf(referencia);
                    if (i < 0) { this.hijos.push(nuevo); } else { this.hijos.splice(i, 0, nuevo); }
                    return nuevo;
                },
                removeChild: function (hijo) {
                    var i = this.hijos.indexOf(hijo);
                    if (i >= 0) { this.hijos.splice(i, 1); }
                    hijo.parentNode = null;
                    return hijo;
                },
                setAttribute: function (nombre, valor) { this.atributos[nombre] = String(valor); },
                getAttribute: function (nombre) { return nombre in this.atributos ? this.atributos[nombre] : null; },
                closest: function () { return this.parentNode; },
                addEventListener: function (tipo, fn) { this.oyentes.push({ tipo: tipo, fn: fn }); }
            };
            // innerHTML no crea hijos aquí: lastChild devuelve un contenedor vacío para lo que el script inserte.
            Object.defineProperty(el, 'lastChild', {
                get: function () { return this.hijos.length ? this.hijos[this.hijos.length - 1] : this.appendChild(__elemento('div')); }
            });
            __elementos.push(el);
            return el;
        }

        function __conectado(el) {
            while (el && !el.esRaiz) { el = el.parentNode; }
            return !!el;
        }

        var document = {
            readyState: 'loading',
            createElement: __elemento,
            addEventListener: function (tipo, fn, opciones) {
                var captura = opciones === true || !!(opciones && opciones.capture);
                __oyentesDelDocumento.push({ tipo: tipo, fn: fn, captura: captura });
            },
            getElementById: function (id) {
                for (var i = 0; i < __elementos.length; i++) {
                    if (__elementos[i].id === id && __conectado(__elementos[i])) { return __elementos[i]; }
                }
                return null;
            },
            querySelector: function () { return null; },
            querySelectorAll: function () { return []; }
        };

        function __crearFormulario(id, campos, atributos) {
            var formulario = __elemento('form');
            formulario.id = id;
            formulario.esRaiz = true;
            formulario.elements = {};
            Object.keys(campos).forEach(function (nombre) {
                var grupo = __elemento('div');
                grupo.className = 'pf-v5-c-form__group';
                var campo = __elemento('input');
                campo.name = nombre;
                campo.value = campos[nombre];
                grupo.appendChild(campo);
                formulario.appendChild(grupo);
                formulario.elements[nombre] = campo;
            });
            var boton = __elemento('button');
            boton.name = 'login';
            formulario.appendChild(boton);
            formulario.elements['login'] = boton;
            Object.keys(atributos).forEach(function (nombre) { formulario.setAttribute(nombre, atributos[nombre]); });
            formulario.querySelector = function (selector) {
                var nombre = /name="([^"]+)"/.exec(selector);
                return nombre ? (this.elements[nombre[1]] || null) : null;
            };
        }

        function __terminarDeCargar() {
            document.readyState = 'interactive';
            __oyentesDelDocumento.forEach(function (o) { if (o.tipo === 'DOMContentLoaded') { o.fn(); } });
        }

        /** Devuelve true si el formulario sale (nadie llamó a preventDefault). */
        function __enviar(id) {
            var formulario = document.getElementById(id);
            var evento = {
                type: 'submit', target: formulario, defaultPrevented: false, detenido: false,
                preventDefault: function () { this.defaultPrevented = true; },
                stopPropagation: function () { this.detenido = true; },
                stopImmediatePropagation: function () { this.detenido = true; }
            };
            function avisar(oyentes, enCaptura) {
                oyentes.forEach(function (o) {
                    if (o.tipo === 'submit' && !!o.captura === enCaptura && !evento.detenido) { o.fn(evento); }
                });
            }
            avisar(__oyentesDelDocumento, true);
            avisar(formulario.oyentes, false);
            avisar(__oyentesDelDocumento.filter(function (o) { return !o.captura; }), false);
            return !evento.defaultPrevented;
        }
        """;

    private readonly Engine motor;
    private readonly string idDelFormulario;

    private PaginaDelTema(string idDelFormulario, string campos, string atributos)
    {
        this.idDelFormulario = idDelFormulario;
        motor = new Engine();
        motor.Execute(DomDeMentira);
        // Como en el navegador: el script corre en el head, antes de que exista el formulario.
        motor.Execute(File.ReadAllText(ScriptDelTema.Ruta));
        motor.Execute($"__crearFormulario('{idDelFormulario}', {campos}, {atributos});");
    }

    /// <summary>La pantalla de ingreso del realm clientes, todavía cargando.</summary>
    public static PaginaDelTema DeIngreso() =>
        new(IdDeIngreso, "{ username: '', password: '' }", "{}");

    /// <summary>La pantalla de cambio de clave del cliente <paramref name="usuario"/>, todavía cargando.</summary>
    public static PaginaDelTema DeCambioDeClave(string usuario)
    {
        var pagina = new PaginaDelTema(IdDeCambioDeClave, "{ 'password-new': '', 'password-confirm': '' }", "{}");
        pagina.motor.SetValue("__usuario", usuario);
        pagina.motor.Execute($"document.getElementById('{IdDeCambioDeClave}').setAttribute('data-usuario', __usuario);");
        return pagina;
    }

    /// <summary>Dispara DOMContentLoaded: lo que el script prepara al terminar de construirse la página.</summary>
    public void TerminarDeCargar() => motor.Execute("__terminarDeCargar();");

    public void Escribir(string campo, string texto)
    {
        motor.SetValue("__campo", campo);
        motor.SetValue("__texto", texto);
        motor.Execute($"document.getElementById('{idDelFormulario}').elements[__campo].value = __texto;");
    }

    /// <summary>Elige el tipo en el selector que el script añade al cargar la pantalla de ingreso.</summary>
    public void ElegirTipo(string tipo)
    {
        motor.SetValue("__tipo", tipo);
        motor.Execute("document.getElementById('tipo-documento').value = __tipo;");
    }

    /// <summary>Envía el formulario. Devuelve true si sale hacia Keycloak y false si el script lo cancela.</summary>
    public bool Enviar() => motor.Evaluate($"__enviar('{idDelFormulario}')").AsBoolean();

    public string Valor(string campo)
    {
        motor.SetValue("__campo", campo);
        return motor.Evaluate($"document.getElementById('{idDelFormulario}').elements[__campo].value").AsString();
    }
}
