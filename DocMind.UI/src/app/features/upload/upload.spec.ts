import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Upload } from './upload';

describe('Upload', () => {
  let component: Upload;
  let fixture: ComponentFixture<Upload>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Upload],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Upload);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
