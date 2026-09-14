const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const markup = fs.readFileSync(path.join(__dirname, '../../src/Bingo.Web/Pages/Admin/Events/Board.cshtml'), 'utf8');
const controller = markup.match(/function syncObjectiveKinds\(\) \{[\s\S]*?\n\}(?=\nfunction initializeRequirements)/)?.[0]
    .replace(/@Json.Serialize\(T\["([^"]+)"\]\.Value\)/g, (_, value) => JSON.stringify(value));
assert.ok(controller, 'Exercise the actual objective-kind controller');
const kind = value => ({
    value, options: [{ value: 'drops' }, { value: 'challenge' }],
    querySelectorAll() { return this.options; },
    setCustomValidity(message) { this.validationMessage = message; }
});
let kinds = [kind('drops')];
const manualInput = { value: '99' };
const section = {};
const context = vm.createContext({ document: {
    querySelectorAll() { return kinds; },
    getElementById() { return manualInput; },
    querySelector() { return section; }
} });
vm.runInContext(controller, context);
const sync = () => vm.runInContext('syncObjectiveKinds()', context);
sync();
assert.equal(manualInput.disabled, true, 'An old manual value cannot be posted for a drop tile');
assert.equal(section.hidden, true);
assert.ok(kinds[0].options.every(option => !option.disabled), 'A single objective can change kind');
kinds = [kind('challenge'), kind('challenge')]; sync();
assert.equal(manualInput.disabled, false);
assert.equal(manualInput.required, true);
assert.equal(manualInput.value, '99', 'Keep the legitimate manual tile total');
assert.ok(kinds.every(control => control.options[0].disabled && !control.options[1].disabled), 'Multiple objectives stay the same kind');
kinds = [kind('drops'), kind('challenge')]; sync();
assert.deepEqual(kinds.map(control => control.value), ['drops', 'challenge'], 'Retained mixed inputs are never silently converted');
assert.ok(kinds.every(control => control.validationMessage.includes('separate tiles')));
assert.equal(manualInput.disabled, true);
kinds.pop(); sync();
assert.equal(kinds[0].validationMessage, '', 'Removing the conflicting objective restores validity');
assert.ok(kinds[0].options.every(option => !option.disabled));
console.log('Board objective kind controls passed.');
