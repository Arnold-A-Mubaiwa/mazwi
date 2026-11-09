import test from 'node:test';
import assert from 'node:assert/strict';
import { inspectProject } from '../.codex/skills/repo-quality/scripts/architecture.mjs';

test('domain cannot reference infrastructure projects or packages', () => {
  const failures = inspectProject('Domain', '<ProjectReference Include="../Mazwi.Repository/Mazwi.Repository.csproj"/><PackageReference Include="Dapper"/>', []);
  assert.equal(failures.length, 2);
});

test('SQL outside Repository is rejected', () => {
  assert.equal(inspectProject('WebApi', '<ProjectReference Include="../Mazwi.Application/Mazwi.Application.csproj"/><ProjectReference Include="../Mazwi.Repository/Mazwi.Repository.csproj"/>', [['Controller.cs', '"SELECT id FROM task_items"']]).length, 1);
});

test('parameterized SQL is allowed and interpolated SQL is rejected', () => {
  const xml = '<ProjectReference Include="../Mazwi.Domain/Mazwi.Domain.csproj"/>';
  assert.deepEqual(inspectProject('Repository', xml, [['Repository.cs', '"SELECT id FROM task_items WHERE id = @Id"']]), []);
  assert.equal(inspectProject('Repository', xml, [['Repository.cs', '$"SELECT id FROM task_items WHERE id = {id}"']]).length, 1);
});
