import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CategoryTargetCard, type TargetCategory } from './CategoryTarget';
import { targetSegments, type CategoryTarget, type CategoryTargetResult, type TargetProgress } from './categoryTargets';

const money = (value: number) => new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' }).format(value);
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
const path = '/api/budgets/b1/categories/groceries/target';

const savedTarget: CategoryTarget = { kind: 'monthly', amount: 100, behavior: 'setAside', weekday: null, dayOfMonth: null, dueDate: null, repeatEvery: null, repeatUnit: null,
  startMonth: '2026-10', version: 3, createdAt: '2026-10-01T00:00:00Z', updatedAt: '2026-10-01T00:00:00Z' };
const progress: TargetProgress = { needed: 100, funded: 30, moreNeeded: 70, segments: 1, fundedFraction: 0.3, status: 'underfunded', nextDueDate: '2026-10-31', monthsRemaining: 1, isPastDue: false, cadence: 'Every month by the last day' };

let requests: { path: string; method: string; body: Record<string, unknown> | null }[];
let reply: (path: string, body: Record<string, unknown> | null) => Response | Promise<Response>;

beforeEach(() => {
  requests = [];
  reply = (_, body) => json({ categoryId: 'groceries', month: '2026-10', target: { ...savedTarget, ...body, version: 1 }, progress } satisfies CategoryTargetResult);
  vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
    const body = init?.body ? JSON.parse(String(init.body)) as Record<string, unknown> : null;
    requests.push({ path: url, method: init?.method ?? 'GET', body });
    return reply(url, body);
  }));
});
afterEach(() => vi.unstubAllGlobals());

function renderCard(category: Partial<TargetCategory> = {}) {
  const onChanged = vi.fn();
  render(<CategoryTargetCard budgetId="b1" month="2026-10" money={money} onChanged={onChanged}
    category={{ id: 'groceries', name: 'Groceries', icon: '▤', categoryTarget: null, targetProgress: null, ...category }} />);
  return onChanged;
}

async function openEditor() {
  await userEvent.click(screen.getByRole('button', { name: 'Create Target' }));
  return screen.getByRole('form', { name: 'Category target' });
}

async function typeAmount(editor: HTMLElement, label: string, value: string) {
  const input = within(editor).getByLabelText(label);
  await userEvent.click(input);
  // MoneyField selects its text in a requestAnimationFrame after focus; let that frame run first, otherwise on a
  // slow runner it fires mid-typing and the next keystroke replaces the digits already typed.
  await new Promise(resolve => requestAnimationFrame(() => resolve(undefined)));
  await userEvent.clear(input);
  await userEvent.type(input, value);
}

describe('CategoryTargetCard', () => {
  it('shows the empty state and saves a monthly set-aside target by default', async () => {
    const onChanged = renderCard();
    expect(screen.getByRole('button', { name: 'Target' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText(/How much do you need for/)).toHaveTextContent('How much do you need for ▤Groceries?');
    expect(screen.getByText(/we’ll let you know how much money to set aside/)).toBeInTheDocument();

    const editor = await openEditor();
    expect(within(editor).getByRole('tab', { name: 'Monthly' })).toHaveAttribute('aria-selected', 'true');
    expect(within(editor).getByLabelText('I need')).toHaveValue('€0.00');
    expect(within(editor).getByLabelText('By')).toHaveDisplayValue('Last Day of Month');
    expect(within(editor).getByRole('button', { name: 'Next month I want to' })).toHaveTextContent('Set aside another €0.00');
    expect(within(editor).queryByRole('button', { name: /Delete/ })).not.toBeInTheDocument();

    await typeAmount(editor, 'I need', '120.50');
    await userEvent.selectOptions(within(editor).getByLabelText('By'), '15th');
    await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(requests[0].path).toBe(path);
    expect(requests[0].method).toBe('PUT');
    expect(requests[0].body).toMatchObject({ version: 0, month: '2026-10', kind: 'monthly', amount: 120.5, behavior: 'setAside', dayOfMonth: 15, weekday: null, dueDate: null, repeatEvery: null, repeatUnit: null });
    expect(typeof requests[0].body?.commandId).toBe('string');
  });

  it('chooses Refill up to from the custom popover with the keyboard', async () => {
    renderCard();
    const editor = await openEditor();
    await typeAmount(editor, 'I need', '80');
    within(editor).getByLabelText('I need').blur();
    const picker = within(editor).getByRole('button', { name: 'Next month I want to' });
    await userEvent.click(picker);
    const list = within(editor).getByRole('listbox', { name: 'Next month I want to' });
    const [setAside, refill] = within(list).getAllByRole('option');
    expect(setAside).toHaveAttribute('aria-selected', 'true');
    expect(setAside).toHaveTextContent('Set aside another €80.00');
    expect(setAside).toHaveTextContent('Use for: bills, subscriptions, saving over time');
    expect(setAside).toHaveTextContent('Most people choose this');
    expect(refill).toHaveTextContent('Refill up to €80.00');
    expect(refill).toHaveTextContent('Use for: gasoline, fun money, dining out');
    expect(refill).toHaveTextContent("Sets a Target to have €80.00 on hand each month. Whatever you don't spend will get applied toward next month's €80.00.");

    fireEvent.keyDown(setAside, { key: 'ArrowDown' });
    expect(refill).toHaveFocus();
    fireEvent.keyDown(refill, { key: 'Enter' });
    expect(within(editor).queryByRole('listbox')).not.toBeInTheDocument();
    expect(picker).toHaveTextContent('Refill up to €80.00');

    await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0].body).toMatchObject({ kind: 'monthly', amount: 80, behavior: 'refill', dayOfMonth: null });
  });

  it('saves a weekly target with the chosen weekday', async () => {
    renderCard();
    const editor = await openEditor();
    await userEvent.click(within(editor).getByRole('tab', { name: 'Weekly' }));
    const every = within(editor).getByLabelText('Every');
    expect(within(every).getAllByRole('option').map(option => option.textContent)).toEqual(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']);
    await typeAmount(editor, 'I need', '25');
    await userEvent.selectOptions(every, 'Friday');
    await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0].body).toMatchObject({ kind: 'weekly', amount: 25, weekday: 5, behavior: 'setAside', dayOfMonth: null, dueDate: null });
  });

  it('saves a yearly target picked from the calendar', async () => {
    renderCard();
    const editor = await openEditor();
    await userEvent.click(within(editor).getByRole('tab', { name: 'Yearly' }));
    expect(within(editor).getByLabelText('By')).toHaveValue('01-11-2026');
    expect(within(editor).getByRole('button', { name: 'Next year I want to' })).toBeInTheDocument();
    await typeAmount(editor, 'I need', '1200');
    await userEvent.click(within(editor).getByRole('button', { name: 'Choose by date' }));
    const calendar = within(editor).getByRole('dialog', { name: 'By calendar' });
    expect(calendar).toHaveTextContent('Nov 2026');
    expect(within(calendar).getAllByText(/^(Su|Mo|Tu|We|Th|Fr|Sa)$/)).toHaveLength(7);
    await userEvent.click(within(calendar).getByRole('button', { name: 'Next month' }));
    await userEvent.click(within(calendar).getByRole('button', { name: '15 December 2026' }));
    expect(within(editor).getByLabelText('By')).toHaveValue('15-12-2026');
    await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0].body).toMatchObject({ kind: 'yearly', amount: 1200, dueDate: '2026-12-15', behavior: 'setAside', repeatEvery: null });
  });

  it('saves a repeating custom fill-up target and validates typed dates', async () => {
    renderCard();
    const editor = await openEditor();
    await userEvent.click(within(editor).getByRole('tab', { name: 'Custom' }));
    await typeAmount(editor, 'Amount', '300');
    within(editor).getByLabelText('Amount').blur();
    await userEvent.click(within(editor).getByRole('button', { name: 'I want to' }));
    await userEvent.click(within(editor).getByRole('option', { name: /Fill up to €300.00/ }));
    const due = within(editor).getByLabelText('Due on');
    await userEvent.clear(due);
    await userEvent.type(due, '31-02-2027');
    await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
    expect(within(editor).getByRole('alert')).toHaveTextContent('Enter a valid date as dd-mm-yyyy.');
    expect(requests).toHaveLength(0);

    await userEvent.clear(due);
    await userEvent.type(due, '20-01-2027');
    const repeat = within(editor).getByRole('switch', { name: 'Repeat' });
    expect(repeat).toHaveAttribute('aria-checked', 'false');
    await userEvent.click(repeat);
    expect(repeat).toHaveAttribute('aria-checked', 'true');
    await userEvent.selectOptions(within(editor).getByLabelText('Repeat interval'), '2');
    await userEvent.selectOptions(within(editor).getByLabelText('Repeat unit'), 'week');
    await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0].body).toMatchObject({ kind: 'custom', amount: 300, behavior: 'refill', dueDate: '2027-01-20', repeatEvery: 2, repeatUnit: 'week' });
  });

  it('rejects a zero amount without sending a request', async () => {
    renderCard();
    const editor = await openEditor();
    await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
    expect(within(editor).getByRole('alert')).toHaveTextContent('Enter an amount greater than zero');
    expect(requests).toHaveLength(0);
  });

  it('summarizes a saved target and edits, cancels and deletes it', async () => {
    const onChanged = renderCard({ categoryTarget: savedTarget, targetProgress: progress });
    const card = screen.getByRole('region', { name: 'Groceries target' });
    expect(card).toHaveTextContent('Set aside another €100.00 each month');
    expect(card).toHaveTextContent('€70.00 more needed this month');
    expect(within(card).getByRole('progressbar', { name: 'Target progress' })).toHaveAttribute('aria-valuenow', '30');

    await userEvent.click(within(card).getByRole('button', { name: 'Edit Target' }));
    let editor = screen.getByRole('form', { name: 'Category target' });
    expect(within(editor).getByLabelText('I need')).toHaveValue('€100.00');
    await userEvent.click(within(editor).getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('form', { name: 'Category target' })).not.toBeInTheDocument();
    expect(requests).toHaveLength(0);

    await userEvent.click(screen.getByRole('button', { name: 'Edit Target' }));
    editor = screen.getByRole('form', { name: 'Category target' });
    reply = () => json({ categoryId: 'groceries', month: '2026-10', target: null, progress: null });
    await userEvent.click(within(editor).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(onChanged).toHaveBeenCalledWith({ categoryId: 'groceries', month: '2026-10', target: null, progress: null }));
    expect(requests[0]).toMatchObject({ path: `${path}/delete`, method: 'POST', body: { version: 3, month: '2026-10' } });
  });

  it('asks for a reload after a version conflict', async () => {
    const onChanged = renderCard({ categoryTarget: savedTarget, targetProgress: progress });
    reply = () => json({ error: 'This target changed in another session.' }, 409);
    await userEvent.click(screen.getByRole('button', { name: 'Edit Target' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save Target' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('This target changed in another session. Reload it before saving again.');
    reply = () => json({ categoryId: 'groceries', month: '2026-10', target: { ...savedTarget, amount: 150, version: 4 }, progress });
    await userEvent.click(screen.getByRole('button', { name: 'Reload target' }));
    await waitFor(() => expect(onChanged).toHaveBeenCalledWith(expect.objectContaining({ target: expect.objectContaining({ version: 4 }) })));
    expect(requests.at(-1)).toMatchObject({ path: `${path}/2026-10`, method: 'GET' });
  });

  it('retries a lost save with the same command', async () => {
    const onChanged = renderCard();
    reply = () => { throw new TypeError('Network down'); };
    const editor = await openEditor();
    await typeAmount(editor, 'I need', '50');
    await userEvent.click(within(editor).getByRole('button', { name: 'Save Target' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('The target could not be confirmed.');
    expect(within(editor).getByRole('button', { name: 'Cancel' })).toBeEnabled();
    reply = (_, body) => json({ categoryId: 'groceries', month: '2026-10', target: { ...savedTarget, amount: 50, version: 1, ...(body ?? {}) }, progress });
    await userEvent.click(screen.getByRole('button', { name: 'Retry save' }));
    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(requests).toHaveLength(2);
    expect(requests[1].body?.commandId).toBe(requests[0].body?.commandId);
  });
});

describe('targetSegments', () => {
  it('splits funding across weekly occurrence segments', () => {
    expect(targetSegments({ ...progress, segments: 5, fundedFraction: 0.5 })).toEqual([1, 1, 0.5, 0, 0]);
    expect(targetSegments({ ...progress, segments: 4, fundedFraction: 1 })).toEqual([1, 1, 1, 1]);
    expect(targetSegments({ ...progress, segments: 1, fundedFraction: 0.3 })).toEqual([0.3]);
  });
});
