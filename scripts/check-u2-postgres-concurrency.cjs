// Brief72 item8: three complete Integration runs with concurrent owned UI review.
const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path');
const {spawn, execFileSync} = require('node:child_process');
const root = process.cwd(), output = path.join(root, 'artifacts/u2-rem2-integration');
fs.mkdirSync(output, {recursive:true});
function run(command,args,log) {
  const started = new Date().toISOString(), stream = fs.createWriteStream(log);
  const child = spawn(command,args,{cwd:root,env:process.env,stdio:['ignore','pipe','pipe']});
  let passedLines=0, tail='';
  for(const source of [child.stdout,child.stderr])source.on('data',chunk=>{
    stream.write(chunk);const text=chunk.toString();passedLines+=(text.match(/\bPassed Bingo\./g)||[]).length;tail=(tail+text).slice(-5000);
  });
  const done = new Promise((resolve,reject)=>{
    child.on('error',reject);
    child.on('close',(code,signal)=>stream.end(()=>resolve({started,finished:new Date().toISOString(),code,signal,log,tail,passedLines})));
  });
  return {child,done,started,progress:()=>passedLines};
}
(async()=>{
  const results=[];
  for(let index=1;index<=3;index++){
    const directory=path.join(output,'run-'+index);fs.mkdirSync(directory,{recursive:true});
    const sourceSha=execFileSync('git',['rev-parse','HEAD'],{cwd:root,encoding:'utf8'}).trim();
    const integrationArgs=['test','tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj','--configuration','Release','--no-build','--no-restore','--results-directory',directory,'--logger','trx;LogFileName=integration.trx','--logger','console;verbosity=normal'];
    const integration=run('dotnet',integrationArgs,path.join(directory,'integration.log'));
    const reviewArgs=['scripts/ui-review.py',index===1?'create':'refresh',index===1?'live':'final-review'];
    const review=run('python3',reviewArgs,path.join(directory,'ui-review.log'));
    console.log('START whole Integration '+index+' with concurrent '+reviewArgs.join(' '));
    const clock=setInterval(()=>console.log('RUN '+index+' observed passed lines='+integration.progress()+'; elapsed '+Math.round((Date.now()-Date.parse(integration.started))/1000)+'s'),30000);
    let tests,ui;
    try{[tests,ui]=await Promise.all([integration.done,review.done]);}finally{clearInterval(clock);}
    const trx=path.join(directory,'integration.trx'), xml=fs.existsSync(trx)?fs.readFileSync(trx,'utf8'):'';
    const tag=xml.match(/<Counters\b[^>]+>/)?.[0]||'';
    const counters=Object.fromEntries([...tag.matchAll(/(\w+)="(\d+)"/g)].map(match=>[match[1],Number(match[2])]));
    const result={index,sourceSha,workingTreeCandidate:true,integrationCommand:['dotnet',...integrationArgs],reviewCommand:['python3',...reviewArgs],tests,ui,trx,counters};
    results.push(result);fs.writeFileSync(path.join(output,'results.json'),JSON.stringify(results,null,2)+'\n');
    console.log(JSON.stringify({index,testsCode:tests.code,uiCode:ui.code,counters,trx,started:tests.started,finished:tests.finished}));
    if(tests.code!==0)console.log(tests.tail);if(ui.code!==0)console.log(ui.tail);
    assert.equal(tests.code,0,'Whole Integration run failed, not retried');
    assert.equal(ui.code,0,'Concurrent review command failed, not retried');
    assert.ok(counters.total>0);assert.equal(counters.failed,0);assert.equal(counters.notExecuted,0);
    assert.equal(counters.passed,counters.total);
    assert.ok(Date.parse(ui.started)<Date.parse(tests.finished)&&Date.parse(tests.started)<Date.parse(ui.finished),'Processes must overlap');
  }
  console.log('PASS: all three whole Integration runs, no failed/skipped, concurrent review create/refresh.');
})().catch(error=>{console.error(error);process.exitCode=1;});
