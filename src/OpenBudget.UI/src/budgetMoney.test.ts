import { expect,it } from 'vitest';
import { parseAssignment } from './budgetMoney';

it.each([['50',50],['50,25',50.25],['-10.01',-10.01],['',0],['0',0],['999999999999.99',999999999999.99]])('accepts exact budget input %s',(input,expected)=>expect(parseAssignment(String(input))).toBe(expected));
it.each(['abc','12abc','1e3','1.001','1,234.56','1000000000000','Infinity','--2'])('rejects invalid budget input %s without converting it to zero',input=>expect(parseAssignment(input)).toBeNull());
