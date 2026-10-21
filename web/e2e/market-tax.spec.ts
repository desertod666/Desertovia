import {test,expect} from '@playwright/test'

type MarketWindow={economyCalls:{requestId:string;taxQuoteId?:string}[];taxQuoteCalls:{requestId:string;lotId:string}[];failAfterMarketCommitOnce:boolean;staleMarketQuoteOnce:boolean}

test.beforeEach(async({page})=>{
  await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
  await page.goto('/e2e/economy-harness.html?tax=1')
  await page.getByText('Кошелёк, имущество и рынок',{exact:true}).click()
  await page.getByText('Рынок',{exact:true}).click()
})

test('an uncertain purchase retries the same quote and trade identity without spending twice',async({page})=>{
  await page.evaluate(()=>{(window as unknown as MarketWindow).failAfterMarketCommitOnce=true})
  const buy=page.getByRole('button',{name:'Купить',exact:true})
  await buy.click();await expect(page.getByRole('alert')).toContainText('Обрыв связи')
  await buy.click();await expect(page.locator('.economy-balances')).toContainText('900')
  const state=await page.evaluate(()=>({trades:(window as unknown as MarketWindow).economyCalls,quotes:(window as unknown as MarketWindow).taxQuoteCalls}))
  expect(state.trades).toHaveLength(2);expect(state.quotes).toHaveLength(1)
  expect(state.trades[0]!.requestId).toBe(state.trades[1]!.requestId)
  expect(state.trades[0]!.taxQuoteId).toBe(state.trades[1]!.taxQuoteId)
  expect(state.trades[0]!.taxQuoteId).toBe(state.quotes[0]!.requestId)
  await expect(page.locator('.economy-panel')).not.toContainText('10%, 20%, 30%')
})

test('an explicit stale-quote rejection allows a fresh quote only on the next click',async({page})=>{
  await page.evaluate(()=>{(window as unknown as MarketWindow).staleMarketQuoteOnce=true})
  const buy=page.getByRole('button',{name:'Купить',exact:true})
  await buy.click();await expect(page.getByRole('alert')).toContainText('устарел')
  await expect(page.locator('.economy-balances')).toContainText('1000')
  expect(await page.evaluate(()=>(window as unknown as MarketWindow).taxQuoteCalls.length)).toBe(1)
  await buy.click();await expect(page.locator('.economy-balances')).toContainText('900')
  const state=await page.evaluate(()=>({trades:(window as unknown as MarketWindow).economyCalls,quotes:(window as unknown as MarketWindow).taxQuoteCalls}))
  expect(state.quotes).toHaveLength(2);expect(state.trades).toHaveLength(2)
  expect(state.trades[0]!.requestId).not.toBe(state.trades[1]!.requestId)
  expect(state.trades[0]!.taxQuoteId).not.toBe(state.trades[1]!.taxQuoteId)
})
