import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RouteReuseStrategy, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { NotebookRouteReuse } from './notebook-route-reuse';

@Component({ template: '' })
class NotebookPage {}

describe('NotebookRouteReuse', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'notebooks/:notebookId', component: NotebookPage }]),
        { provide: RouteReuseStrategy, useClass: NotebookRouteReuse },
      ],
    });
  });

  it('creates a new component when going to another notebook', async () => {
    const harness = await RouterTestingHarness.create();

    const first = await harness.navigateByUrl('/notebooks/a');
    const second = await harness.navigateByUrl('/notebooks/b');

    expect(second).toBeInstanceOf(NotebookPage);
    expect(second).not.toBe(first);
  });

  it('keeps the component when the notebook stays the same', async () => {
    const harness = await RouterTestingHarness.create();

    const first = await harness.navigateByUrl('/notebooks/a');
    const second = await harness.navigateByUrl('/notebooks/a?tab=sources');

    expect(second).toBe(first);
  });
});
