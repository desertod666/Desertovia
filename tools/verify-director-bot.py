from pathlib import Path
import argparse,importlib.util,json,unittest,uuid,sys,hashlib,datetime
parser=argparse.ArgumentParser(description="Run existing mocked bot tests with an exported synthetic game observation; no live model or Telegram.")
parser.add_argument('--bot-repo',type=Path,required=True)
parser.add_argument('--snapshot',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
repo=args.bot_repo.resolve();sys.path.insert(0,str(repo))
spec=importlib.util.spec_from_file_location('candidate_almighty_tests',repo/'tests/test_almighty.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
payload=args.snapshot.read_text(encoding='utf-8').strip()
snapshot=json.loads(payload)
assert snapshot['contract']['version']==1 and len(payload)<=36000

class CandidateBridgeTest(module.AlmightyTest):
 async def test_actual_candidate_contract_reaches_mock_model_is_cached_and_cannot_grant_a_new_tool(self):
  body={'kind':'analyze','worldId':snapshot['contract']['worldId'],'requestId':snapshot['contract']['observationId'],'snapshot':payload}
  reply=await self.client.post('/director',json=body)
  self.assertEqual(reply.status,200);self.assertEqual(await reply.json(),self.answer)
  self.assertEqual(json.loads(self.llm.chat.call_args.args[1][0]['content']),snapshot)
  self.assertEqual(await (await self.client.post('/director',json=body)).json(),self.answer)
  self.assertEqual(self.llm.chat.await_count,1)
  body['requestId']=str(uuid.uuid4());self.llm.chat.return_value=json.dumps(dict(self.answer,option='wallet_credit'))
  reply=await self.client.post('/director',json=body)
  self.assertEqual(reply.status,200);self.assertEqual((await reply.json())['errorCode'],'model.option')
  self.bot.send_message.assert_not_awaited()

suite=unittest.defaultTestLoader.loadTestsFromModule(module)
suite.addTest(CandidateBridgeTest('test_actual_candidate_contract_reaches_mock_model_is_cached_and_cannot_grant_a_new_tool'))
result=unittest.TextTestRunner(verbosity=1).run(suite)
receipt={'recorded_at_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'tests':result.testsRun,'failures':len(result.failures),'errors':len(result.errors),'skipped':len(result.skipped),'python':sys.version.split()[0],'actual_snapshot_characters':len(payload),'options':len(snapshot['options']),'capability_kinds':len(snapshot['permissions']),'snapshot_sha256':hashlib.sha256(payload.encode()).hexdigest(),'scope':'Existing bot HTTP/parser/cache tests plus exact exported synthetic C#v1 observation through existing bot handle; AI/Telegram mocked, no paid models or real messages','bot_changed':False,'source_sha256':{p.relative_to(repo).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in [repo/'sramoslav/almighty.py',repo/'tests/test_almighty.py']}}
args.output.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(receipt,ensure_ascii=False))
raise SystemExit(0 if result.wasSuccessful() else 1)
