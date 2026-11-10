import test from 'node:test';
import assert from 'node:assert/strict';
import { inspectProject } from '../.codex/skills/repo-quality/scripts/architecture.mjs';

test('domain cannot reference infrastructure projects or packages', () => {
  const failures = inspectProject('Domain', '<ProjectReference Include="../Mazwi.Repository/Mazwi.Repository.csproj"/><PackageReference Include="Dapper"/>', []);
  assert.equal(failures.length, 2);
});

test('SQL outside Repository is rejected', () => {
  assert.equal(inspectProject('WebApi', '<ProjectReference Include="../Mazwi.Application/Mazwi.Application.csproj"/><ProjectReference Include="../Mazwi.Repository/Mazwi.Repository.csproj"/><ProjectReference Include="../Mazwi.Ai/Mazwi.Ai.csproj"/>', [['Controller.cs', '"SELECT id FROM task_items"']]).length, 1);
});

test('Ai references Domain only, forbids packages, SQL and outward code dependencies', () => {
  const xml = '<ProjectReference Include="../Mazwi.Domain/Mazwi.Domain.csproj"/>';
  assert.deepEqual(inspectProject('Ai', xml, [['Adapter.cs', 'using System.Net.Http; using Mazwi.Domain;']]), []);
  for (const code of ['using Mazwi.Application;', 'using Npgsql;', 'SELECT id FROM stories']) {
    assert.notEqual(inspectProject('Ai', xml, [['Adapter.cs', code]]).length, 0);
  }
  assert.notEqual(inspectProject('Ai', xml + '<PackageReference Include="OpenAI"/>', []).length, 0);
});

test('parameterized SQL is allowed and interpolated SQL is rejected', () => {
  const xml = '<ProjectReference Include="../Mazwi.Domain/Mazwi.Domain.csproj"/>';
  assert.deepEqual(inspectProject('Repository', xml, [['Repository.cs', '"SELECT id FROM task_items WHERE id = @Id"']]), []);
  assert.equal(inspectProject('Repository', xml, [['Repository.cs', '$"SELECT id FROM task_items WHERE id = {id}"']]).length, 1);
});
