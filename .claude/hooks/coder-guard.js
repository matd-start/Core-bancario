// Hook PreToolUse del subagente coder.
// Bloquea que el coder edite pruebas, specs, ADRs o la configuración de Claude.
// Claude Code envía el evento como JSON por stdin; salir con código 2 bloquea la herramienta
// y el texto de stderr le llega al coder como motivo.
const path = require('path');

let raw = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => (raw += chunk));
process.stdin.on('end', () => {
  let input;
  try {
    input = JSON.parse(raw);
  } catch {
    process.exit(0); // Sin JSON válido no decidimos nada: sigue el flujo normal de permisos.
  }

  const target = input.tool_input?.file_path ?? input.tool_input?.notebook_path;
  if (!target) process.exit(0);

  const root = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();
  const relative = path.relative(root, path.resolve(root, target)).split(path.sep).join('/');
  const lower = relative.toLowerCase();
  const segments = lower.split('/');

  // Dentro de specs/ el coder solo puede marcar tareas y escribir sus notas de aprendizaje.
  const allowedInSpecs = /^specs\/[^/]+\/(tasks|learning)\.md$/;

  const isTest = segments[0] === 'tests' || segments.some(s => s.endsWith('.tests'));
  const isSpec = segments[0] === 'specs' && !allowedInSpecs.test(lower);
  const isAdr = lower.startsWith('docs/adr/');
  const isClaudeConfig = segments[0] === '.claude';

  if (isTest || isSpec || isAdr || isClaudeConfig) {
    process.stderr.write(
      `Bloqueado por el flujo SDD: el coder no puede modificar "${relative}". ` +
        'Si una prueba, la spec o el plan parecen incorrectos, no los cambies: ' +
        'detente y explícalo en tu respuesta final.'
    );
    process.exit(2);
  }

  process.exit(0);
});
